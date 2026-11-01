using Behind.Common;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SessionListEndpoints
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> Get(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        TimeProvider timeProvider,
        [FromQuery] int? activePage = null,
        [FromQuery] int? endedPage = null,
        [FromQuery] int? pageSize = null)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                var sessions = await sessionQueries.GetUserSessions(
                    session.UserId,
                    timeProvider.GetUtcNow(),
                    Flipper.Create(activePage, pageSize, DefaultPageSize, MaxPageSize),
                    Flipper.Create(endedPage, pageSize, DefaultPageSize, MaxPageSize),
                    httpContext.RequestAborted);

                return Results.Json(sessions.ToResponse(session.SessionId));
            });
    }
}
