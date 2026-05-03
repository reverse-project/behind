using System.Text.Json;
using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Core.WebApi.Endpoints;

public static class AuditLogEntryEndpoints
{
    public static async Task<IResult> Get(
        AbstractCoreContext context,
        IAuditLogEntryQueries queries,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] string filter,
        [FromQuery] string orderBy,
        [FromQuery] int page,
        [FromQuery] int pageSize
    )
    {
        var items = await queries.GetAuditLog(
            IAuditLogEntryQueries.RequestAuditLog.Create(
                fromDate,
                toDate,
                filter.Create<AuditLogEntry>(context).Match(() => new EmptyFilter<AuditLogEntry>(), f => f),
                new Flipper(page, pageSize),
                orderBy
            )
        );
        
        return items.Match(() => Results.StatusCode(204), i => Results.Json(i, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}