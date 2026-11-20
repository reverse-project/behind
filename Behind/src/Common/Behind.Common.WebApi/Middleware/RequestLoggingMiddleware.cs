using System.Diagnostics;
using Behind.Common.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Behind.Common.WebApi.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText is { } template
            ? Escape(template)
            : "<unmatched>";

        logger.LogDebug("HTTP {Method} {Path} started", () => [context.Request.Method, route]);

        await next(context);

        logger.Log(
            LevelFor(context.Response.StatusCode),
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
            () =>
            [
                context.Request.Method,
                route,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds
            ]);
    }

    private static string Escape(string value) => string.Concat(value.Select(character =>
        char.IsControl(character) || character is '\u2028' or '\u2029'
            ? $"\\u{(int)character:x4}"
            : character.ToString()));

    private static LogLevel LevelFor(int statusCode) =>
        statusCode switch
        {
            >= StatusCodes.Status500InternalServerError => LogLevel.Error,
            >= StatusCodes.Status400BadRequest => LogLevel.Warning,
            _ => LogLevel.Information
        };
}
