using System.Security.Cryptography;
using Behind.Common.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Behind.Common.WebApi.Middleware;

public sealed class ETagMiddleware(RequestDelegate next, ETagOptions options, ILogger<ETagMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsIgnored(context) || !options.AllowedMethods.Contains(context.Request.Method))
        {
            logger.LogDebug(
                "ETag handling skipped for {Method} {Path}",
                () => [context.Request.Method, context.Request.Path.Value]);

            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        if (!options.AllowedStatusCodes.Contains(context.Response.StatusCode))
        {
            await CopyToAsync(buffer, originalBody, context.RequestAborted);
            return;
        }

        var eTag = ComputeETag(buffer);
        context.Response.Headers.ETag = eTag.ToString();

        if (RequestHasMatchingETag(context.Request, eTag))
        {
            logger.LogDebug(
                "ETag {ETag} matched for {Method} {Path}, responding with {StatusCode}",
                () => [eTag.Tag.Value, context.Request.Method, context.Request.Path.Value, StatusCodes.Status304NotModified]);

            context.Response.StatusCode = StatusCodes.Status304NotModified;
            context.Response.ContentLength = null;
            context.Response.Headers.Remove(HeaderNames.ContentType);
            context.Response.Headers.Remove(HeaderNames.ContentEncoding);
            return;
        }

        await CopyToAsync(buffer, originalBody, context.RequestAborted);
    }

    private static bool IsIgnored(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<IgnoreETagAttribute>() is not null;

    private static Task CopyToAsync(MemoryStream buffer, Stream destination, CancellationToken cancellationToken)
    {
        buffer.Position = 0;
        return buffer.CopyToAsync(destination, cancellationToken);
    }

    private static EntityTagHeaderValue ComputeETag(MemoryStream buffer)
    {
        buffer.Position = 0;
        var hash = SHA256.HashData(buffer);
        return new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(hash)}\"");
    }

    private static bool RequestHasMatchingETag(HttpRequest request, EntityTagHeaderValue eTag)
    {
        var ifNoneMatch = request.Headers.IfNoneMatch;

        return ifNoneMatch.Count > 0
               && EntityTagHeaderValue.TryParseList(ifNoneMatch!, out var requestedTags)
               && requestedTags.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(eTag, useStrongComparison: true));
    }
}
