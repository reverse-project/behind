using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SignOutEndpoints
{
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
        Func<CurrentSession, Task> action)
    {
        var active = await httpContext.FindCurrentSession(sessionQueries);

        return await active.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                await action(session);
                return Results.NoContent();
            });
    }
}
