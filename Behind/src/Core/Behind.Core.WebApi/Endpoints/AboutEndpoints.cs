using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class AboutEndpoints
{
    public static IResult Get() => Results.Json(AboutResponse.Current);
}
