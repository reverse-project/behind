using Behind.Common.Logging;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.MagicLinks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.TwoFactor;

public interface ITwoFactorSignIn
{
    Task<IResult> Complete(
        Guid userId,
        MagicLinkFunction? function,
        IReadOnlyList<string>? recoveryCodes,
        HttpContext httpContext,
        TwoFactorTicket? ticket = null);
}

public sealed class TwoFactorSignIn(
    UserManager<ApplicationUser> userManager,
    IOptions<SecurityConfiguration> security,
    ISessionCommands sessionCommands,
    IWallOfShameQueries wallOfShameQueries,
    IPasswordResetGrants passwordResetGrants,
    IMagicLinkCommands magicLinkCommands,
    TimeProvider timeProvider,
    ILogger<TwoFactorSignIn> logger) : ITwoFactorSignIn
{
    public async Task<IResult> Complete(
        Guid userId,
        MagicLinkFunction? function,
        IReadOnlyList<string>? recoveryCodes,
        HttpContext httpContext,
        TwoFactorTicket? ticket = null)
    {
        if (ticket?.Nonce is not { } nonce || ticket.UserId != userId || ticket.Function != function
            || ticket.Stage is not (TwoFactorStage.Verify or TwoFactorStage.Setup)
            || !await magicLinkCommands.ConsumeForFunction(nonce, userId, MagicLinkFunction.TwoFactorChallenge,
                timeProvider.GetUtcNow(), httpContext.RequestAborted))
        {
            logger.LogWarning("Second-factor challenge rejected for user {UserId}: invalid or already used", () => [userId]);
            return TwoFactorSignInEndpoints.TicketRejected();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Results.Unauthorized();
        }

        return await security.Value.Jwt.Match(
            none: () => SignInEndpoints.NotConfigured(logger, nameof(Complete)),
            jwtSettings => SignInEndpoints.StartSession(
                user,
                jwtSettings,
                security.Value.EmailConfirmation,
                sessionCommands,
                wallOfShameQueries,
                timeProvider.GetUtcNow(),
                httpContext,
                logger,
                None,
                function,
                recoveryCodes is { Count: > 0 } ? recoveryCodes : null,
                passwordResetGrants));
    }
}
