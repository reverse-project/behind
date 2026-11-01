using System.Diagnostics;
using Behind.Common.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Behind.Common.WebApi.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();

        logger.LogDebug("HTTP {Method} {Path} started", () => [context.Request.Method, context.Request.Path.Value]);

        await next(context);

        logger.Log(
            LevelFor(context.Response.StatusCode),
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
            () =>
            [
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds
            ]);
    }

    private static LogLevel LevelFor(int statusCode) =>
        statusCode switch
        {
            >= StatusCodes.Status500InternalServerError => LogLevel.Error,
            >= StatusCodes.Status400BadRequest => LogLevel.Warning,
            _ => LogLevel.Information
        };
}
