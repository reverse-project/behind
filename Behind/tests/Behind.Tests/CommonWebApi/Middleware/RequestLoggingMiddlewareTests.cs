using Behind.Common.WebApi.Middleware;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Behind.Tests.CommonWebApi.Middleware;

public class RequestLoggingMiddlewareTests
{
    private static async Task<CapturingLogger<RequestLoggingMiddleware>> InvokeAsync(int statusCode)
    {
        var logger = new CapturingLogger<RequestLoggingMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/generator/qrcode/REUSABLE_SECRET";
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/generator/qrcode/{data}"), 0, null, null));

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

    [Theory]
    [InlineData(false, "<unmatched>")]
    [InlineData(true, "/configured\\u000d\\u000a\\u0009\\u2028")]
    public async Task InvokeAsync_ControlCharactersCannotCreateLogLines(bool matched, string expectedPath)
    {
        var logger = new CapturingLogger<RequestLoggingMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Path = "/forged\r\nentry\t\u2028";
        if (matched)
        {
            var route = RoutePatternFactory.Parse("/configured");
            route = RoutePatternFactory.Pattern("/configured\r\n\t\u2028", route.PathSegments);
            context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, route, 0, null, null));
        }

        await new RequestLoggingMiddleware(_ => Task.CompletedTask, logger).InvokeAsync(context);
        Assert.All(logger.Entries, entry =>
        {
            Assert.DoesNotContain("\r", entry.Message);
            Assert.DoesNotContain("\n", entry.Message);
            Assert.DoesNotContain("\t", entry.Message);
            Assert.DoesNotContain("\u2028", entry.Message);
            Assert.Equal(expectedPath, entry.State["Path"]);
        });
    }

    [Fact]
    public async Task InvokeAsync_LogsDebugOnStart()
    {
        var logger = await InvokeAsync(StatusCodes.Status200OK);

        var entry = logger.Single(LogLevel.Debug);
        Assert.Equal(HttpMethods.Post, entry.State["Method"]);
        Assert.Equal("/generator/qrcode/{data}", entry.State["Path"]);
        Assert.DoesNotContain(logger.Entries, entry => entry.State.Values.Any(value => value?.ToString()?.Contains("REUSABLE_SECRET") == true));
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
