using Behind.Common;
using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SessionEndpoints
{
    public const string RefreshTokenHeader = "X-Refresh-Token";

    private const string NoStore = "no-store";

    private sealed record ActiveSession(Guid Id, Guid UserId, HumanUser User, ValidSession Session);

    [IgnoreETag]
    public static async Task<IResult> Get(
        HttpContext httpContext,
        IOptions<SecurityConfiguration> securityConfiguration,
        ISessionQueries sessionQueries,
        ISessionCommands sessionCommands,
        IAccessQueries accessQueries,
        TimeProvider timeProvider,
        ILogger<SessionEndpoints> logger)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        return await securityConfiguration.Value.Jwt.Match(
            none: () =>
            {
                logger.LogError("Session lookup rejected: JWT settings are not configured");

                return Task.FromResult(Results.InternalServerError());
            },
            async jwtSettings =>
            {
                var now = timeProvider.GetUtcNow();
                var current = await FindCurrent(httpContext, sessionQueries, now);

                return await current.Match(
                    none: async () =>
                    {
                        logger.LogDebug("No current session for the access token, trying the refresh token");

                        var refreshable = await FindRefreshable(httpContext, jwtSettings, sessionQueries, now);

                        return await refreshable.Match(
                            none: () =>
                            {
                                logger.LogWarning("Session lookup rejected: no valid access or refresh token");

                                return Task.FromResult(Results.Unauthorized());
                            },
                            some: active => Renew(
                                httpContext, jwtSettings, sessionCommands, accessQueries, active, now, logger));
                    },
                    some: active => Respond(httpContext, accessQueries, active, now, null));
            });
    }

    private static async Task<IResult> Renew(
        HttpContext httpContext,
        JwtSettings jwtSettings,
        ISessionCommands sessionCommands,
        IAccessQueries accessQueries,
        ActiveSession active,
        DateTimeOffset now,
        ILogger logger)
    {
        var accessToken = AccessToken.Create(active.User, now, DomainConstants.AccessTokenDuration);

        var renewed = await sessionCommands.RenewAccessToken(
            active.Id,
            active.Session.AccessToken.Nonce,
            accessToken,
            httpContext.RequestAborted);

        if (!renewed)
        {
            logger.LogWarning(
                "Access token renewal for session {SessionId} of user {UserId} conflicted with another renewal",
                () => [active.Id, active.UserId]);

            return Results.Conflict();
        }

        logger.LogInformation(
            "Access token renewed for session {SessionId} of user {UserId}, expires at {AccessTokenExpiresAt}",
            () => [active.Id, active.UserId, accessToken.ExpiresAt]);

        return await Respond(
            httpContext,
            accessQueries,
            active with { Session = active.Session with { AccessToken = accessToken } },
            now,
            new RefreshedAccessToken(
                accessToken.ToJwtToken(active.User.ToApplicationUser(active.UserId), active.Id, jwtSettings),
                accessToken.ExpiresAt));
    }

    private static async Task<IResult> Respond(
        HttpContext httpContext,
        IAccessQueries accessQueries,
        ActiveSession active,
        DateTimeOffset now,
        RefreshedAccessToken? refreshed)
    {
        var access = await accessQueries.GetUserAccess(active.UserId, httpContext.RequestAborted);

        return Results.Json(
            active.Session.ToSessionResponse(active.Id, active.UserId, active.User, access, now, refreshed));
    }

    private static async Task<Option<ActiveSession>> FindCurrent(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        DateTimeOffset now) =>
        await httpContext.User.SessionClaims.Match(
            () => Task.FromResult<Option<ActiveSession>>(None),
            async claims =>
            {
                var found = await Find(httpContext, sessionQueries, claims.SessionId);

                return found.Where(active => active.Session.RejectionFor(claims.Nonce, now) == None);
            });

    private static async Task<Option<ActiveSession>> FindRefreshable(
        HttpContext httpContext,
        JwtSettings jwtSettings,
        ISessionQueries sessionQueries,
        DateTimeOffset now)
    {
        var token = httpContext.Request.Headers[RefreshTokenHeader].ToString();

        if (string.IsNullOrWhiteSpace(token))
        {
            return None;
        }

        var claims = await jwtSettings.ReadSessionClaims(token);

        return await claims.Match(
            () => Task.FromResult<Option<ActiveSession>>(None),
            async c =>
            {
                var found = await Find(httpContext, sessionQueries, c.SessionId);

                return found.Where(active =>
                    active.Session.RefreshToken.Nonce == c.Nonce && active.Session.RefreshToken.ExpiresAt > now);
            });
    }

    private static async Task<Option<ActiveSession>> Find(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        Guid sessionId)
    {
        var found = await sessionQueries.TryFindByIdWithUserId(sessionId, httpContext.RequestAborted);

        return found
            .Where(result => result.Session is ValidSession { User: HumanUser })
            .Map(result =>
            {
                var session = (ValidSession)result.Session;
                return new ActiveSession(sessionId, result.UserId, (HumanUser)session.User, session);
            });
    }
}
