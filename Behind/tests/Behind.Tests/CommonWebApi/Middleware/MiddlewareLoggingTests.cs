using Behind.Common.WebApi.Filters;
using Behind.Common.WebApi.Handlers;
using Behind.Common.WebApi.Middleware;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CommonWebApi.Middleware;

public class MiddlewareLoggingTests
{
    private sealed record Payload([property: System.ComponentModel.DataAnnotations.Required] string? Name);

    [Fact]
    public async Task CorrelationId_WithoutHeader_LogsDebugWithGeneratedId()
    {
        var logger = new CapturingLogger<CorrelationIdMiddleware>();
        var context = new DefaultHttpContext();

        await new CorrelationIdMiddleware(_ => Task.CompletedTask, CorrelationIdOptions.Default, logger)
            .InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal(context.CorrelationId, entry.State["CorrelationId"]);
        Assert.Equal("missing", entry.State["HeaderState"]);
    }

    [Fact]
    public async Task CorrelationId_WithValidHeader_LogsDebugWithReusedId()
    {
        var logger = new CapturingLogger<CorrelationIdMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdOptions.DefaultHeaderName] = "order-42";

        await new CorrelationIdMiddleware(_ => Task.CompletedTask, CorrelationIdOptions.Default, logger)
            .InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal("order-42", entry.State["CorrelationId"]);
    }

    [Fact]
    public async Task CorrelationId_WithInvalidHeader_LogsWarning()
    {
        var logger = new CapturingLogger<CorrelationIdMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdOptions.DefaultHeaderName] = "not valid!";

        await new CorrelationIdMiddleware(_ => Task.CompletedTask, CorrelationIdOptions.Default, logger)
            .InvokeAsync(context);

        var entry = logger.Single(LogLevel.Warning);
        Assert.Equal("invalid", entry.State["HeaderState"]);
    }

    [Fact]
    public async Task ETag_NotAllowedMethod_LogsDebugSkip()
    {
        var logger = new CapturingLogger<ETagMiddleware>();
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/users";

        await new ETagMiddleware(_ => Task.CompletedTask, ETagOptions.Default, logger).InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal("/users", entry.State["Path"]);
    }

    [Fact]
    public async Task ETag_MatchingIfNoneMatch_LogsDebugWithStatus()
    {
        var logger = new CapturingLogger<ETagMiddleware>();
        var body = System.Text.Encoding.UTF8.GetBytes("hello");
        var etag = $"\"{Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(body))}\"";
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.IfNoneMatch = etag;

        await new ETagMiddleware(
            ctx => ctx.Response.Body.WriteAsync(body).AsTask(),
            ETagOptions.Default,
            logger).InvokeAsync(context);

        var entry = logger.Single(LogLevel.Debug);
        Assert.Equal(etag, entry.State["ETag"]);
        Assert.Equal(StatusCodes.Status304NotModified, entry.State["StatusCode"]);
    }

    [Fact]
    public async Task BadRequestHandler_BadHttpRequest_LogsWarningWithException()
    {
        var logger = new CapturingLogger<BadRequestExceptionHandler>();
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var exception = new BadHttpRequestException("bad");

        var handled = await new BadRequestExceptionHandler(logger)
            .TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Same(exception, logger.Single(LogLevel.Warning).Exception);
    }

    [Fact]
    public async Task BadRequestHandler_OtherException_LogsDebugAndDoesNotHandle()
    {
        var logger = new CapturingLogger<BadRequestExceptionHandler>();

        var handled = await new BadRequestExceptionHandler(logger)
            .TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException(), CancellationToken.None);

        Assert.False(handled);
        Assert.Equal("InvalidOperationException", Assert.Single(logger.Entries).State["ExceptionType"]);
    }

    private static EndpointFilterInvocationContext FilterContext(params object?[] arguments) =>
        new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), arguments);

    [Fact]
    public async Task ValidationFilter_ValidPayload_LogsDebug()
    {
        var logger = new CapturingLogger<DataAnnotationValidationFilter<Payload>>();

        await new DataAnnotationValidationFilter<Payload>(logger)
            .InvokeAsync(FilterContext(new Payload("ada")), _ => ValueTask.FromResult<object?>(null));

        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task ValidationFilter_InvalidPayload_LogsWarningWithInvalidMembers()
    {
        var logger = new CapturingLogger<DataAnnotationValidationFilter<Payload>>();

        await new DataAnnotationValidationFilter<Payload>(logger)
            .InvokeAsync(FilterContext(new Payload(null)), _ => ValueTask.FromResult<object?>(null));

        Assert.Equal(["Name"], (string[])logger.Single(LogLevel.Warning).State["InvalidMembers"]!);
    }

    [Fact]
    public async Task ValidationFilter_MissingPayload_LogsWarning()
    {
        var logger = new CapturingLogger<DataAnnotationValidationFilter<Payload>>();

        await new DataAnnotationValidationFilter<Payload>(logger)
            .InvokeAsync(FilterContext(), _ => ValueTask.FromResult<object?>(null));

        Assert.Equal(nameof(Payload), logger.Single(LogLevel.Warning).State["RequestType"]);
    }
}
