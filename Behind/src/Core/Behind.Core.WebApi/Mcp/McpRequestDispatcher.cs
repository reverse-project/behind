using System.Text;
using System.Text.Json;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Mcp;

public sealed class McpRequestDispatcher(
    IServiceScopeFactory scopes,
    IHttpContextAccessor accessor,
    IOptionsMonitor<McpConfiguration> configuration,
    ILogger<McpRequestDispatcher> logger) : IMcpRequestDispatcher
{
    public static bool IsAllowedPath(string path)
    {
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal)
            || path.Contains('\\') || path.Contains('#') || path.Contains('?')
            || path.Contains('%') || path.Split('/').Any(segment => segment is "." or ".."))
        {
            return false;
        }

        var excluded = new[] { "/mcp", "/service-users", "/sign", "/session", "/sessions", "/impersonation",
            "/subaccounts", "/profile" };
        return !excluded.Any(prefix => path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
            && !path.EndsWith("/impersonate", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<McpApiResponse> Dispatch(
        HttpContext caller, JsonElement arguments, CancellationToken cancellationToken)
    {
        var method = arguments.GetProperty("method").GetString()!;
        var path = arguments.GetProperty("path").GetString()!;
        if (!IsAllowedPath(path))
        {
            return new(StatusCodes.Status403Forbidden, "This path is not available through MCP.");
        }

        var pipeline = caller.Features.Get<McpPipeline>()
            ?? throw new InvalidOperationException("MCP pipeline is not installed.");
        var identity = caller.Features.Get<ServiceUserIdentity>()
            ?? throw new InvalidOperationException("A service identity is required.");
        await using var scope = scopes.CreateAsyncScope();
        var request = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            User = identity.Principal,
            RequestAborted = cancellationToken
        };
        request.Features.Set(identity);
        request.Request.Method = method;
        request.Request.Path = path;
        request.Request.Scheme = caller.Request.Scheme;
        request.Request.Host = caller.Request.Host;
        request.Request.PathBase = caller.Request.PathBase;
        request.Connection.RemoteIpAddress = caller.Connection.RemoteIpAddress;
        request.Request.Headers.Accept = "application/json";
        foreach (var header in new[] { "X-Content-Locale", "Accept-Language" })
        {
            request.Request.Headers[header] = caller.Request.Headers[header];
        }

        if (arguments.TryGetProperty("query", out var query))
        {
            request.Request.QueryString = QueryString.Create(
                query.EnumerateObject().Select(item => new KeyValuePair<string, string?>(item.Name, item.Value.GetString())));
        }

        var body = arguments.TryGetProperty("body", out var payload) ? payload.GetRawText() : "";
        request.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        request.Request.ContentLength = request.Request.Body.Length;
        request.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyDetection(body.Length > 0));
        if (body.Length > 0)
        {
            request.Request.ContentType = "application/json";
        }

        await using var response = new SizeLimitedBuffer(configuration.CurrentValue.MaxResponseBytes);
        request.Response.Body = response;
        var previous = accessor.HttpContext;
        accessor.HttpContext = request;
        try
        {
            await pipeline.Invoke(request);
            if (request.Response.StatusCode is >= 300 and < 400
                || request.Response.Headers.ContainsKey("Set-Cookie"))
            {
                return new(StatusCodes.Status403Forbidden, "Interactive authentication is not available through MCP.");
            }

            var contentType = request.Response.ContentType;
            if (response.Length > 0 && (contentType is null
                || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
                && !contentType.StartsWith("application/problem+json", StringComparison.OrdinalIgnoreCase)))
            {
                return new(StatusCodes.Status415UnsupportedMediaType, "Only JSON API responses are available through MCP.");
            }

            logger.LogInformation("Service user {ServiceUserId} invoked {Method} {Path}: {StatusCode}",
                identity.ServiceUser.Id, method, path, request.Response.StatusCode);
            return new(request.Response.StatusCode, Encoding.UTF8.GetString(response.GetBuffer(), 0, (int)response.Length));
        }
        catch (PayloadLimitExceededException exception)
        {
            logger.LogWarning(exception, "MCP API response exceeded its configured limit; the operation may have completed");
            return new(StatusCodes.Status502BadGateway, "Response size limit exceeded. The operation may have completed; do not retry blindly.");
        }
        finally
        {
            accessor.HttpContext = previous;
            await request.Request.Body.DisposeAsync();
        }

    }

    private sealed record RequestBodyDetection(bool CanHaveBody) : IHttpRequestBodyDetectionFeature;
}
