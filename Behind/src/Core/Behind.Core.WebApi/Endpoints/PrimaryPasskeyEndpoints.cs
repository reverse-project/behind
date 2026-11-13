using System.Buffers.Text;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Behind.Common;
using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Captcha;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class PrimaryPasskeyEndpoints
{
    public sealed record OptionsRequest(
        [property: Required, DataType(DataType.EmailAddress)] string Email,
        CaptchaResponse? Captcha = null) : ICaptchaRequest;

    public sealed record AssertionRequest(
        [property: Required] string Ticket,
        [property: Required] AuthenticatorAssertionRawResponse Credential);

    [IgnoreETag]
    public static async Task<IResult> Options(
        OptionsRequest request,
        HttpContext context,
        IConfiguration configuration,
        UserManager<ApplicationUser> users,
        ITwoFactorVerification verification,
        IPasskeyCeremonies ceremonies,
        ITwoFactorTickets tickets,
        IMagicLinkCommands commands,
        TimeProvider timeProvider)
    {
        context.Response.Headers.CacheControl = "no-store";
        var user = await users.FindByEmailAsync(request.Email);
        var origin = configuration.PasskeyOrigin();
        var available = user is null
            ? None
            : await verification.BeginPasskey(origin, user.Id, context.RequestAborted);
        var options = available.GetOrElse(() => ceremonies.BeginAssertion(
            origin, [new PublicKeyCredentialDescriptor(RandomNumberGenerator.GetBytes(32))]));
        options.UserVerification = UserVerificationRequirement.Required;
        var nonce = Guid.NewGuid();

        if (user is not null)
        {
            var now = timeProvider.GetUtcNow();
            await commands.Issue(user.Id,
                new MagicLink(nonce, now, now.Add(TwoFactorTickets.Lifetime), MagicLinkFunction.PasskeyChallenge),
                context.RequestAborted);
        }

        var ticket = tickets.Create(user?.Id ?? Guid.NewGuid(), TwoFactorStage.PrimaryPasskey, ceremony: options.ToJson())
            with { Nonce = nonce, Email = request.Email };
        return Results.Json(new PasskeyAssertionChallenge(tickets.Seal(ticket), options));
    }

    [IgnoreETag]
    public static async Task<IResult> Verify(
        AssertionRequest request,
        HttpContext context,
        IConfiguration configuration,
        UserManager<ApplicationUser> users,
        IOptions<SecurityConfiguration> security,
        ITwoFactorVerification verification,
        ITwoFactorTickets tickets,
        IMagicLinkCommands commands,
        ISessionCommands sessions,
        IWallOfShameQueries bans,
        ITwoFactorGate gate,
        TimeProvider timeProvider,
        ILogger<PrimaryPasskeyEndpoints> logger)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (request.Credential.RawId is null || request.Credential.RawId.Length == 0)
        {
            logger.LogWarning("Primary passkey sign-in rejected: missing credential ID");
            return Results.Unauthorized();
        }

        var opened = tickets.Open(request.Ticket)
            .Where(ticket => ticket.Stage == TwoFactorStage.PrimaryPasskey && ticket.Nonce is not null);
        return await opened.Match(
            () => Task.FromResult<IResult>(TwoFactorSignInEndpoints.TicketRejected()),
            async ticket =>
            {
                var options = Try(() => AssertionOptions.FromJson(ticket.Ceremony ?? string.Empty))
                    .Run().Match<Option<AssertionOptions>>(_ => None, parsed => parsed.ToOption());
                var verified = await options.Match(
                    () => Task.FromResult(false),
                    parsed => verification.VerifyPasskey(configuration.PasskeyOrigin(), ticket.UserId,
                        parsed, request.Credential, context.RequestAborted));
                if (!verified || !await commands.ConsumeForFunction(ticket.Nonce!.Value, ticket.UserId,
                        MagicLinkFunction.PasskeyChallenge, timeProvider.GetUtcNow(), context.RequestAborted))
                {
                    logger.LogWarning("Primary passkey sign-in rejected for user {UserId}", () => [ticket.UserId]);
                    return Results.Unauthorized();
                }

                var user = await users.FindByIdAsync(ticket.UserId.ToString());
                if (user is null || (security.Value.EmailConfirmation
                    && !user.Emails.Any(email => email.ConfirmedAt is not null
                        && string.Equals(email.Value, ticket.Email?.Trim(), StringComparison.OrdinalIgnoreCase))))
                {
                    return Results.Unauthorized();
                }

                return await security.Value.Jwt.Match(
                    () => SignInEndpoints.NotConfigured(logger, nameof(Verify)),
                    jwt => SignInEndpoints.StartSession(user, jwt, security.Value.EmailConfirmation, sessions,
                        bans, timeProvider.GetUtcNow(), context, logger, Some(gate),
                        primaryCredentialId: Base64Url.EncodeToString(request.Credential.RawId)));
            });
    }
}
