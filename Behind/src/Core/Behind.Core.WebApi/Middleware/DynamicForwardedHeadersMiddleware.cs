using Behind.Core.WebApi.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Middleware;

public sealed class DynamicForwardedHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, IOptionsMonitor<ForwardedHeadersPolicy> policy)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var chain))
        {
            var client = policy.CurrentValue.Resolve(context.Connection.RemoteIpAddress, chain);
            if (client is not null)
            {
                context.Connection.RemoteIpAddress = client;
                context.Connection.RemotePort = 0;
            }
        }
        return next(context);
    }
}
