using Behind.Common.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Behind.Core.WebApi.Captcha;

public static class CaptchaEndpoints
{
    private const string NoStore = "no-store";

    [IgnoreETag]
    public static IResult GetChallenge(ICaptchaService captchaService, HttpContext httpContext)
    {
        httpContext.Response.Headers[HeaderNames.CacheControl] = NoStore;

        return Results.Json(captchaService.Create());
    }
}
