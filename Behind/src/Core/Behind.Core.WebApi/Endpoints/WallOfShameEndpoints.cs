using Behind.Common;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Core.WebApi.Endpoints;

public sealed class WallOfShameEndpoints
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    [IgnoreETag]
    public static async Task<IResult> Get(
        HttpContext httpContext,
        IWallOfShameQueries queries,
        TimeProvider timeProvider,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        var shamed = await queries.GetActive(
            Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
            timeProvider.GetUtcNow(),
            httpContext.RequestAborted);

        return Results.Json(shamed.ToPageResponse(user => user.ToItem()));
    }
}
