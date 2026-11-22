using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SubaccountFeatureFilter(bool requiresLinking) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        var settings = http.RequestServices.GetRequiredService<IOptions<SecurityConfiguration>>().Value.Accounts;
        return settings.Subaccounts && (!requiresLinking || settings.LinkUnlink)
            ? next(context)
            : ValueTask.FromResult<object?>(Results.NotFound());
    }
}
