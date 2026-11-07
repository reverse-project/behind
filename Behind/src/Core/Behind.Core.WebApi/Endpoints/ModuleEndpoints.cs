using Behind.Common.Modules;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class ModuleEndpoints
{
    public const string Route = "/modules";

    public static IResult Get(ModuleCatalog catalog) => Results.Json(catalog.ToResponse());
}
