using Behind.Common;
using Behind.Common.WebApi.Middleware;
using Behind.Sport.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Sport.WebApi.Endpoints;

public static class DetailEndpoints
{
    public const int DefaultLeaderboardSize = 10;
    public const int MaxLeaderboardSize = 100;

    public static Task<IResult> Discipline(Guid id, HttpContext httpContext, ISportDetailQueries queries) =>
        Found(queries.Discipline(id, httpContext.RequestAborted));

    public static Task<IResult> Team(Guid id, HttpContext httpContext, ISportDetailQueries queries) =>
        Found(queries.Team(id, httpContext.RequestAborted));

    public static Task<IResult> Player(Guid id, HttpContext httpContext, ISportDetailQueries queries) =>
        Found(queries.Player(id, httpContext.RequestAborted));

    public static Task<IResult> Season(Guid id, HttpContext httpContext, ISportDetailQueries queries) =>
        Found(queries.Season(id, httpContext.RequestAborted));

    [IgnoreETag]
    public static Task<IResult> Leaderboards(
        Guid seasonId,
        HttpContext httpContext,
        ISportDetailQueries queries,
        [FromQuery] int? limit = null) =>
        Found(queries.Leaderboards(seasonId, Limit(limit), httpContext.RequestAborted));

    [IgnoreETag]
    public static Task<IResult> Leaderboard(
        Guid seasonId,
        Guid statTypeId,
        HttpContext httpContext,
        ISportDetailQueries queries,
        [FromQuery] int? limit = null) =>
        Found(queries.Leaderboard(seasonId, statTypeId, Limit(limit), httpContext.RequestAborted));

    private static int Limit(int? limit) =>
        Math.Clamp(limit ?? DefaultLeaderboardSize, 1, MaxLeaderboardSize);

    private static async Task<IResult> Found<T>(Task<Option<T>> detail) =>
        (await detail).Match(() => Results.NotFound(), found => Results.Json(found));
}
