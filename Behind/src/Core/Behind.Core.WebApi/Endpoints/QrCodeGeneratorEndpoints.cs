using Behind.Common;
using Behind.Common.WebApi.Middleware;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class QrCodeGeneratorEndpoints
{
    public const string SvgContentType = "image/svg+xml";

    [IgnoreETag]
    public static IResult Get(HttpContext httpContext, string data)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        return data
            .FromBase64Text()
            .Bind(text => text.ToQrCodeSvg())
            .Match(
                () => Results.BadRequest(),
                svg => Results.Content(svg, SvgContentType));
    }
}
