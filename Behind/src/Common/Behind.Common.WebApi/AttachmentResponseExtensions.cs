using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Behind.Common.WebApi;

public static class AttachmentResponseExtensions
{
    public const string DefaultContentType = "application/octet-stream";
    public const string Revalidate = "no-cache";

    extension(HttpContext httpContext)
    {
        public IResult ToAttachment(string fileName, string contentType, byte[] content)
        {
            httpContext.Response.Headers.CacheControl = Revalidate;
            httpContext.Response.Headers[HeaderNames.XContentTypeOptions] = ImageResponseExtensions.NoSniff;

            return Results.File(content, contentType, fileName);
        }
    }
}
