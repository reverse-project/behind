using Behind.Common;
using Behind.Common.Logging;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class ImpersonationEndpoints
{
    private const string NoStore = "no-store";

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> Start(
        Guid id,
        HttpContext httpContext,
        UserManagementGate gate,
        UserManager<ApplicationUser> userManager,
        IOptions<SecurityConfiguration> securityConfiguration,
        IAccessQueries accessQueries,
        ISessionCommands sessionCommands,
        ILogger<ImpersonationEndpoints> logger)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        return securityConfiguration.Value.Jwt.Match(
            none: () =>
            {
                logger.LogError("Impersonation rejected: JWT settings are not configured");

                return Task.FromResult<IResult>(Results.InternalServerError());
            },
            jwtSettings => gate.RunForResult(
                httpContext,
                id,
                (actor, target) => actor.CanImpersonate(target),
                (actor, target, now) => Begin(
                    actor, target, now, jwtSettings, httpContext, userManager, accessQueries, sessionCommands, logger)));
    }

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> Stop(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        ISessionCommands sessionCommands,
        TimeProvider timeProvider,
        ILogger<ImpersonationEndpoints> logger)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () =>
            {
                logger.LogWarning("Impersonation stop rejected: no active session");

                return Task.FromResult<IResult>(Results.Unauthorized());
            },
            async session =>
            {
                var actor = await sessionQueries.TryFindActor(session.SessionId, httpContext.RequestAborted);
                var impersonatorId = actor.Bind(found => found.ImpersonatorId);

                return await impersonatorId.Match(
                    () =>
                    {
                        logger.LogWarning(
                            "Impersonation stop rejected: session {SessionId} is not an impersonation session",
                            () => [session.SessionId]);

                        return Task.FromResult<IResult>(Results.Conflict());
                    },
                    async impersonator =>
                    {
                        await sessionCommands.InvalidateSession(
                            session.SessionId,
                            impersonator,
                            timeProvider.GetUtcNow(),
                            httpContext.RequestAborted);

                        logger.LogInformation(
                            "User {ImpersonatorId} stopped impersonating user {UserId} in session {SessionId}",
                            () => [impersonator, session.UserId, session.SessionId]);

                        return Results.NoContent();
                    });
            });
    }

    private static async Task<IResult> Begin(
        UserManagementActor actor,
        IUserManagementQueries.ManagedUser target,
        DateTimeOffset now,
        JwtSettings jwtSettings,
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        IAccessQueries accessQueries,
        ISessionCommands sessionCommands,
        ILogger logger)
    {
        var targetAccess = await accessQueries.GetUserAccess(target.Id, httpContext.RequestAborted);

        if (!actor.CanGrant(targetAccess.CalculatedGrants))
        {
            logger.LogWarning(
                "User {ImpersonatorId} may not impersonate user {UserId}: the user holds grants the actor does not",
                () => [actor.UserId, target.Id]);

            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var user = await userManager.FindByIdAsync(target.Id.ToString());

        if (user is null)
        {
            logger.LogWarning("Impersonation rejected: user {UserId} no longer exists", () => [target.Id]);

            return Results.NotFound();
        }

        var humanUser = user.ToHumanUser();
        var accessToken = AccessToken.Create(humanUser, now, DomainConstants.AccessTokenDuration);
        var refreshToken = RefreshToken.Create(humanUser, now, DomainConstants.ImpersonationDuration);

        var sessionId = await sessionCommands.CreateImpersonation(
            actor.UserId,
            target.Id,
            new ValidSession(
                accessToken,
                refreshToken,
                httpContext.ClientAddress(RemoteAddress.MaxLength),
                httpContext.ClientUserAgent(UserAgent.MaxLength)),
            httpContext.RequestAborted);

        logger.LogInformation(
            "User {ImpersonatorId} started impersonating user {UserId} in session {SessionId}, expires at {ExpiresAt}",
            () => [actor.UserId, target.Id, sessionId, refreshToken.ExpiresAt]);

        return Results.Json(user.ToSignInResponse(accessToken, refreshToken, sessionId, jwtSettings));
    }
}
