using Behind.Common.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Captcha;

public sealed record CaptchaRejectedResponse(string Code = CaptchaRejectedResponse.InvalidCode)
{
    public const string InvalidCode = "captchaInvalid";
}

public sealed class CaptchaFilter<T>(ICaptchaService captchaService, ILogger<CaptchaFilter<T>> logger)
    : IEndpointFilter where T : class, ICaptchaRequest
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();

        if (request is not null && captchaService.Verify(request.Captcha))
        {
            return await next(context);
        }

        logger.LogWarning(
            "Rejected {Method} {Path}: the captcha is missing, expired, used or wrong",
            () => [context.HttpContext.Request.Method, context.HttpContext.Request.Path.Value]);

        return Results.BadRequest(new CaptchaRejectedResponse());
    }
}
