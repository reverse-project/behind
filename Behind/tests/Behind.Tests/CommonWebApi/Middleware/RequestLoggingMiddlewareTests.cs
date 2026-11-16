using Behind.Common.WebApi.Middleware;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CommonWebApi.Middleware;

public class RequestLoggingMiddlewareTests
{
    private static async Task<CapturingLogger<RequestLoggingMiddleware>> InvokeAsync(int statusCode)
    {
        var logger = new CapturingLogger<RequestLoggingMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/sign/out";

        var middleware = new RequestLoggingMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = statusCode;
                return Task.CompletedTask;
            },
            logger);

        await middleware.InvokeAsync(context);

        return logger;
    }

    [Fact]
    public async Task InvokeAsync_LogsDebugOnStart()
    {
        var logger = await InvokeAsync(StatusCodes.Status200OK);

        var entry = logger.Single(LogLevel.Debug);
        Assert.Equal(HttpMethods.Post, entry.State["Method"]);
        Assert.Equal("/sign/out", entry.State["Path"]);
    }

    [Theory]
    [InlineData(StatusCodes.Status200OK, LogLevel.Information)]
    [InlineData(StatusCodes.Status304NotModified, LogLevel.Information)]
    [InlineData(StatusCodes.Status401Unauthorized, LogLevel.Warning)]
    [InlineData(StatusCodes.Status404NotFound, LogLevel.Warning)]
    [InlineData(StatusCodes.Status500InternalServerError, LogLevel.Error)]
    public async Task InvokeAsync_LogsCompletionAtLevelMatchingStatusCode(int statusCode, LogLevel expected)
    {
        var logger = await InvokeAsync(statusCode);

        var entry = Assert.Single(logger.Entries, e => e.Level != LogLevel.Debug);
        Assert.Equal(expected, entry.Level);
        Assert.Equal(statusCode, entry.State["StatusCode"]);
        Assert.IsType<double>(entry.State["ElapsedMilliseconds"]);
    }

    [Fact]
    public async Task InvokeAsync_EndpointThrows_PropagatesWithoutCompletionLog()
    {
        var logger = new CapturingLogger<RequestLoggingMiddleware>();
        var middleware = new RequestLoggingMiddleware(_ => throw new InvalidOperationException(), logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(new DefaultHttpContext()));

        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Entries).Level);
    }
}
