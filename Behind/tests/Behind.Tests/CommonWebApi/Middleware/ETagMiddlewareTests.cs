using System.Security.Cryptography;
using System.Text;
using Behind.Common.WebApi.Middleware;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CommonWebApi.Middleware;

public class ETagMiddlewareTests
{
    private static async Task<DefaultHttpContext> InvokeAsync(
        string method,
        Func<HttpContext, Task> endpoint,
        string? ifNoneMatch = null,
        ETagOptions? options = null,
        Endpoint? matchedEndpoint = null)
    {
        var context = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() }
        };
        context.Request.Method = method;

        if (ifNoneMatch is not null)
        {
            context.Request.Headers.IfNoneMatch = ifNoneMatch;
        }

        if (matchedEndpoint is not null)
        {
            context.SetEndpoint(matchedEndpoint);
        }

        var middleware = new ETagMiddleware(ctx => endpoint(ctx), options ?? ETagOptions.Default);
        await middleware.InvokeAsync(context);

        return context;
    }

    private static Endpoint IgnoreETagEndpoint() =>
        new(requestDelegate: null, new EndpointMetadataCollection(new IgnoreETagAttribute()), "ignored-endpoint");

    private static Task WriteOk(HttpContext context, string body)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(body);
    }

    private static string Sha256ETagOf(string body)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(body));
        return $"\"{Convert.ToHexStringLower(hash)}\"";
    }

    [Fact]
    public async Task InvokeAsync_GetWithoutIfNoneMatch_SetsETagHeaderAndReturnsBody()
    {
        const string body = "hello world";

        var context = await InvokeAsync(HttpMethods.Get, ctx => WriteOk(ctx, body));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(Sha256ETagOf(body), context.Response.Headers.ETag.ToString());

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        Assert.Equal(body, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task InvokeAsync_GetWithMatchingIfNoneMatch_Returns304WithEmptyBody()
    {
        const string body = "hello world";
        var eTag = Sha256ETagOf(body);

        var context = await InvokeAsync(HttpMethods.Get, ctx => WriteOk(ctx, body), ifNoneMatch: eTag);

        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
        Assert.Equal(eTag, context.Response.Headers.ETag.ToString());
        Assert.Equal(0, context.Response.Body.Length);
        Assert.True(string.IsNullOrEmpty(context.Response.ContentType));
    }

    [Fact]
    public async Task InvokeAsync_GetWithWildcardIfNoneMatch_Returns304()
    {
        var context = await InvokeAsync(HttpMethods.Get, ctx => WriteOk(ctx, "hello world"), ifNoneMatch: "*");

        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_GetWithStaleIfNoneMatch_Returns200WithBody()
    {
        const string body = "hello world";

        var context = await InvokeAsync(HttpMethods.Get, ctx => WriteOk(ctx, body), ifNoneMatch: "\"stale-etag\"");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(Sha256ETagOf(body), context.Response.Headers.ETag.ToString());

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        Assert.Equal(body, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task InvokeAsync_DisallowedMethod_PassesThroughWithoutETag()
    {
        const string body = "created";

        var context = await InvokeAsync(HttpMethods.Post, ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            return ctx.Response.WriteAsync(body);
        });

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
        Assert.True(string.IsNullOrEmpty(context.Response.Headers.ETag.ToString()));

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        Assert.Equal(body, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task InvokeAsync_DisallowedStatusCode_PassesThroughWithoutETag()
    {
        const string body = "not found";

        var context = await InvokeAsync(HttpMethods.Get, ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return ctx.Response.WriteAsync(body);
        });

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.True(string.IsNullOrEmpty(context.Response.Headers.ETag.ToString()));

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        Assert.Equal(body, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task InvokeAsync_EndpointMarkedIgnoreETag_PassesThroughWithoutETag()
    {
        const string body = "ignored";

        var context = await InvokeAsync(
            HttpMethods.Get,
            ctx => WriteOk(ctx, body),
            matchedEndpoint: IgnoreETagEndpoint());

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(string.IsNullOrEmpty(context.Response.Headers.ETag.ToString()));

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        Assert.Equal(body, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task InvokeAsync_EndpointMarkedIgnoreETag_IgnoresMatchingIfNoneMatch()
    {
        const string body = "ignored";
        var eTag = Sha256ETagOf(body);

        var context = await InvokeAsync(
            HttpMethods.Get,
            ctx => WriteOk(ctx, body),
            ifNoneMatch: eTag,
            matchedEndpoint: IgnoreETagEndpoint());

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(string.IsNullOrEmpty(context.Response.Headers.ETag.ToString()));

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        Assert.Equal(body, await reader.ReadToEndAsync());
    }
}
