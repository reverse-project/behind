using Behind.Common.Modules;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Behind.Common.WebApi.Middleware;

public sealed record ModuleEndpoint(string Key);

public sealed class ModuleAvailabilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ModuleAvailability availability,
        ModuleCatalog catalog,
        IConfiguration configuration,
        ILogger<ModuleAvailabilityMiddleware> logger)
    {
        var key = context.GetEndpoint()?.Metadata.GetMetadata<ModuleEndpoint>()?.Key;
        if (key is null)
        {
            key = catalog.Modules.FirstOrDefault(module =>
                context.Request.Path.StartsWithSegments($"/modules/{module.Key}"))?.Key;
        }

        if (key is null)
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        if (!availability.IsEnabled(key))
        {
            logger.LogInformation("Request rejected because module {Module} is disabled", key);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var original = context.RequestAborted;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(original);
        using var changes = ChangeToken.OnChange(configuration.GetReloadToken, () =>
        {
            if (!availability.IsEnabled(key))
            {
                cancellation.Cancel();
            }
        });
        if (!availability.IsEnabled(key))
        {
            logger.LogInformation("Request rejected because module {Module} is disabled", key);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        context.RequestAborted = cancellation.Token;
        try
        {
            await next(context);
        }
        finally
        {
            context.RequestAborted = original;
        }
    }
}
