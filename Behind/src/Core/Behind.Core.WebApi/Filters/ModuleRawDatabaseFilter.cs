using Behind.Common.Database.Modules;
using Behind.Common.Database.Raw;
using Behind.Common.Modules;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Filters;

public sealed class ModuleRawDatabaseFilter(
    ModuleAvailability availability,
    ModuleCatalog catalog,
    AbstractCoreContext context,
    ILogger<ModuleRawDatabaseFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        if (invocation.HttpContext.Request.RouteValues["table"] is string table
            && context.FindRawTable(table).Match(() => false, raw => !availability.IsEntityEnabled(catalog, raw.EntityName)))
        {
            logger.LogInformation("Raw table request rejected because its module is disabled");
            return Results.NotFound();
        }

        var result = await next(invocation);
        return result is JsonHttpResult<RawTablesResponse> { Value: { } response }
            ? Results.Json(response with
            {
                Tables = [..response.Tables.Where(table => availability.IsEntityEnabled(catalog, table.EntityName))]
            })
            : result;
    }
}
