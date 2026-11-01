using Behind.Common.WebApi.Middleware;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CommonWebApi.Middleware;

public class CorrelationIdMiddlewareTests
{
    private static async Task<(DefaultHttpContext Context, string? SeenByEndpoint)> InvokeAsync(
        string? headerValue = null,
        CorrelationIdOptions? options = null)
    {
        var context = new DefaultHttpContext();
        var resolved = options ?? CorrelationIdOptions.Default;

        if (headerValue is not null)
        {
            context.Request.Headers[resolved.HeaderName] = headerValue;
        }

        string? seen = null;
        var middleware = new CorrelationIdMiddleware(ctx =>
        {
            seen = ctx.CorrelationId;
            return Task.CompletedTask;
        }, resolved, NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        return (context, seen);
    }

    [Fact]
    public async Task InvokeAsync_WithoutHeader_GeneratesGuidV7()
    {
        var (context, seen) = await InvokeAsync();

        var generated = context.Response.Headers[CorrelationIdOptions.DefaultHeaderName].ToString();
        Assert.True(Guid.TryParse(generated, out var guid));
        Assert.Equal(7, guid.Version);
        Assert.Equal(generated, seen);
        Assert.Equal(generated, context.CorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_WithHeader_ReusesValueAndEchoesIt()
    {
        var (context, seen) = await InvokeAsync("order-42");

        Assert.Equal("order-42", seen);
        Assert.Equal("order-42", context.Response.Headers[CorrelationIdOptions.DefaultHeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_WithPaddedHeader_TrimsValue()
    {
        var (_, seen) = await InvokeAsync("  abc-123  ");

        Assert.Equal("abc-123", seen);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData("bad<script>")]
    public async Task InvokeAsync_WithInvalidHeader_GeneratesNewValue(string header)
    {
        var (_, seen) = await InvokeAsync(header);

        Assert.True(Guid.TryParse(seen, out var guid));
        Assert.Equal(7, guid.Version);
    }

    [Fact]
    public async Task InvokeAsync_WithTooLongHeader_GeneratesNewValue()
    {
        var options = CorrelationIdOptions.Default with { MaxLength = 5 };

        var (_, seen) = await InvokeAsync("123456", options);

        Assert.True(Guid.TryParse(seen, out _));
    }

    [Fact]
    public async Task InvokeAsync_WithCustomHeaderAndGenerator_UsesThem()
    {
        var options = new CorrelationIdOptions { HeaderName = "X-Request-Id", Generator = () => "generated" };

        var (context, seen) = await InvokeAsync(options: options);

        Assert.Equal("generated", seen);
        Assert.Equal("generated", context.Response.Headers["X-Request-Id"].ToString());
    }

    [Fact]
    public void CorrelationId_WithoutMiddleware_IsNull()
    {
        Assert.Null(new DefaultHttpContext().CorrelationId);
    }
}
