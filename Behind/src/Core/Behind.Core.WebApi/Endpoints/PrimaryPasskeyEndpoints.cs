using System.Buffers.Text;
using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Captcha;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.MagicLinks;
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
        var options = ceremonies.BeginAssertion(origin, []);
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
            with
        { Nonce = nonce, Email = request.Email };
        return Results.Json(new PasskeyAssertionChallenge(tickets.Seal(ticket), options));
    }

    public sealed record LegacyLinkRequest([property: Required] string Ticket);

    [IgnoreETag]
    public static async Task<IResult> RequestLegacyLink(
        OptionsRequest request, HttpContext context, IConfiguration configuration,
        UserManager<ApplicationUser> users, ITwoFactorTickets tickets, IMagicLinkCommands commands,
        IMagicLinkMailQueue mailQueue, IMagicLinkQueries queries, IMagicLinkSettingsStore settings,
        TimeProvider timeProvider)
    {
        context.Response.Headers.CacheControl = "no-store";
        var user = await users.FindByEmailAsync(request.Email);
        var address = user?.Emails.FirstOrDefault(email => email.ConfirmedAt is not null
            && string.Equals(email.Value, request.Email.Trim(), StringComparison.OrdinalIgnoreCase));
        var now = timeProvider.GetUtcNow();
        var cooldown = settings.Current.Map(value => value.EffectiveRequestCooldown)
            .GetOrElse(MagicLinkSettings.DefaultRequestCooldown);
        if (user is not null && address is not null
            && !await queries.HasIssuedSince(user.Id, now - cooldown, context.RequestAborted))
        {
            var link = MagicLink.Create(now, TwoFactorTickets.Lifetime, MagicLinkFunction.LegacyPasskeyLink);
            await commands.Issue(user.Id, link, context.RequestAborted);
            var ticket = tickets.Create(user.Id, TwoFactorStage.LegacyPasskeyLink)
                with
            { Nonce = link.Nonce.Nonce, Email = address.Value };
            var url = configuration.PasskeyOrigin().ToFrontendLink("/sign-in", [new("legacyPasskey", tickets.Seal(ticket))]);
            mailQueue.TryEnqueue(new MagicLinkMail(user, address.Value, url, MagicLinkFunction.LegacyPasskeyLink));
        }

        return Results.NoContent();
    }

    [IgnoreETag]
    public static async Task<IResult> LegacyOptions(
        LegacyLinkRequest request, HttpContext context, IConfiguration configuration,
        ITwoFactorTickets tickets, IMagicLinkCommands commands, ITwoFactorVerification verification,
        TimeProvider timeProvider)
    {
        context.Response.Headers.CacheControl = "no-store";
        var opened = tickets.Open(request.Ticket)
            .Where(ticket => ticket.Stage == TwoFactorStage.LegacyPasskeyLink && ticket.Nonce.HasValue);
        return await opened.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async ticket =>
            {
                var now = timeProvider.GetUtcNow();
                if (!await commands.ConsumeForFunction(ticket.Nonce!.Value, ticket.UserId,
                        MagicLinkFunction.LegacyPasskeyLink, now, context.RequestAborted))
                {
                    return Results.Unauthorized();
                }

                var assertion = await verification.BeginPasskey(configuration.PasskeyOrigin(), ticket.UserId, context.RequestAborted);
                return await assertion.Match(
                    () => Task.FromResult<IResult>(Results.Unauthorized()),
                    async options =>
                    {
                        options.UserVerification = UserVerificationRequirement.Required;
                        var challenge = MagicLink.Create(now, TwoFactorTickets.Lifetime, MagicLinkFunction.PasskeyChallenge);
                        await commands.Issue(ticket.UserId, challenge, context.RequestAborted);
                        var next = tickets.Create(ticket.UserId, TwoFactorStage.PrimaryPasskey, ceremony: options.ToJson())
                            with
                        { Nonce = challenge.Nonce.Nonce, Email = ticket.Email };
                        return Results.Json(new PasskeyAssertionChallenge(tickets.Seal(next), options));
                    });
            });
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
