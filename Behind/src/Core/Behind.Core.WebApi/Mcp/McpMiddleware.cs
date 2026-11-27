using System.Net.Http.Headers;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Mcp;

public sealed record McpPipeline(RequestDelegate Invoke);

public sealed class McpMiddleware(RequestDelegate next, ILogger<McpMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IServiceUserStore users,
        IOptionsMonitor<McpConfiguration> options,
        IOptionsMonitor<AccountsConfiguration> accounts,
        TimeProvider timeProvider)
    {
        context.Features.Set(new McpPipeline(next));
        if (!string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/mcp", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        var configuration = options.CurrentValue;
        if (!configuration.Enabled || !accounts.CurrentValue.ServiceUser)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var origins = context.Request.Headers.Origin;
        if (origins.Count > 0 && (origins.Count != 1
            || !configuration.AllowedOrigins.Contains(origins[0], StringComparer.Ordinal)))
        {
            logger.LogWarning("Rejected MCP request with an unapproved Origin");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization.ToString(), out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var user = await users.Authenticate(header.Parameter, context.RequestAborted);
        await user.Match(
            () =>
            {
                logger.LogWarning("Rejected MCP request with invalid service credentials");
                context.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            async found =>
            {
                var identity = ServiceUserIdentity.Create(found, timeProvider.GetUtcNow());
                context.Features.Set(identity);
                context.User = identity.Principal;
                context.Request.Headers.Remove("Authorization");
                await next(context);
            });
    }
}
