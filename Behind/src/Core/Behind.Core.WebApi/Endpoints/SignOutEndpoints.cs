using Behind.Common.Logging;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SignOutEndpoints
{
    [Authorize]
    [IgnoreETag]
    public static Task<IResult> SignOut(
        HttpContext httpContext,
        ISessionCommands sessionCommands,
        ISessionQueries sessionQueries,
        TimeProvider timeProvider,
        ILogger<SignOutEndpoints> logger) =>
        WithActiveSession(
            httpContext,
            sessionQueries,
            logger,
            nameof(SignOut),
            active => sessionCommands.InvalidateSession(
                active.SessionId,
                active.UserId,
                timeProvider.GetUtcNow(),
                httpContext.RequestAborted));

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> SignOutAll(
        HttpContext httpContext,
        ISessionCommands sessionCommands,
        ISessionQueries sessionQueries,
        TimeProvider timeProvider,
        ILogger<SignOutEndpoints> logger) =>
        WithActiveSession(
            httpContext,
            sessionQueries,
            logger,
            nameof(SignOutAll),
            active => sessionCommands.InvalidateAllSessions(
                active.UserId,
                active.UserId,
                timeProvider.GetUtcNow(),
                httpContext.RequestAborted));

    private static async Task<IResult> WithActiveSession(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        ILogger logger,
        string operation,
        Func<CurrentSession, Task> action)
    {
        var active = await httpContext.FindCurrentSession(sessionQueries);

        return await active.Match(
            () =>
            {
                logger.LogWarning("{Operation} rejected: no active session", () => [operation]);

                return Task.FromResult<IResult>(Results.Unauthorized());
            },
            async session =>
            {
                await action(session);

                logger.LogInformation(
                    "{Operation} completed for session {SessionId} of user {UserId}",
                    () => [operation, session.SessionId, session.UserId]);

                return Results.NoContent();
            });
    }
}
