using static Behind.Common.FunctionalExtensions;
using Behind.Common;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SignOutEndpoints
{
    private sealed record ActiveSession(Guid SessionId, Guid UserId);

    [Authorize]
    [IgnoreETag]
    public static Task<IResult> SignOut(
        HttpContext httpContext,
        ISessionCommands sessionCommands,
        ISessionQueries sessionQueries,
        TimeProvider timeProvider) =>
        WithActiveSession(
            httpContext,
            sessionQueries,
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
        TimeProvider timeProvider) =>
        WithActiveSession(
            httpContext,
            sessionQueries,
            active => sessionCommands.InvalidateAllSessions(
                active.UserId,
                active.UserId,
                timeProvider.GetUtcNow(),
                httpContext.RequestAborted));

    private static async Task<IResult> WithActiveSession(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        Func<ActiveSession, Task> action)
    {
        var active = await FindActiveSession(httpContext, sessionQueries);

        return await active.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                await action(session);
                return Results.NoContent();
            });
    }

    private static async Task<Option<ActiveSession>> FindActiveSession(
        HttpContext httpContext,
        ISessionQueries sessionQueries) =>
        await httpContext.User.SessionId.Match(
            () => Task.FromResult<Option<ActiveSession>>(None),
            async sessionId =>
            {
                var found = await sessionQueries.TryFindByIdWithUserId(sessionId, httpContext.RequestAborted);

                return found
                    .Where(result => result.Session is ValidSession)
                    .Map(result => new ActiveSession(sessionId, result.UserId));
            });
}
