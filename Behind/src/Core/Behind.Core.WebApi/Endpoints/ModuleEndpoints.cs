using Behind.Common.Modules;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class ModuleEndpoints
{
    public const string Route = "/modules";

    public static IResult Get(ModuleCatalog catalog, AccessCatalog access, ModuleAvailability availability, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Json(catalog.ToResponse(access, availability));
    }
}
