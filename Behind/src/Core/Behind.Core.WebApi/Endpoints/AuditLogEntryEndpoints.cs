using Behind.Common;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Core.WebApi.Endpoints;

public static class AuditLogEntryEndpoints
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 200;
    public const int DefaultRangeDays = 7;
    public const string NewestFirst = "audit.Timestamp DESC";
    public const string OldestFirst = "audit.Timestamp ASC";

    [Authorize]
    [IgnoreETag]
    public static async Task<IResult> Get(
        HttpContext httpContext,
        IAuditLogEntryQueries queries,
        TimeProvider timeProvider,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] string? search = null,
        [FromQuery] bool oldestFirst = false,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var to = toDate ?? today;
        var from = fromDate ?? to.AddDays(-DefaultRangeDays);

        if (from > to)
        {
            return Results.BadRequest();
        }

        var flipper = Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize);

        var entries = await queries.GetAuditLog(
            IAuditLogEntryQueries.RequestAuditLog.Create(
                from,
                to,
                search.ToSearchTerm(MaxSearchLength).ToAuditLogFilter(),
                flipper,
                oldestFirst ? OldestFirst : NewestFirst),
            httpContext.RequestAborted);

        return Results.Json(entries.Match(
            () => new PageResponse<AuditLogItem>(0, flipper.Index, 0, flipper.Size, []),
            paged => paged.ToPageResponse(entry => entry.ToItem())));
    }
}
