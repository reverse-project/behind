using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Behind.Common.WebApi;

public static class ImageResponseExtensions
{
    public const string ImmutableCache = "public, max-age=31536000, immutable";
    public const string NoSniff = "nosniff";

    extension(HttpContext httpContext)
    {
        public IResult ToImmutableImage(string contentType, byte[] content)
        {
            httpContext.Response.Headers.CacheControl = ImmutableCache;
            httpContext.Response.Headers[HeaderNames.XContentTypeOptions] = NoSniff;

            return Results.File(content, contentType);
        }
    }
}
