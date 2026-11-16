using Behind.Common.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Behind.Core.WebApi.Captcha;

public static class CaptchaExtensions
{
    public const string ChallengeRoute = "/captcha";
    public const string RateLimitPolicy = "captcha";
    public const int ChallengeLimit = 30;
    public static readonly TimeSpan ChallengeWindow = TimeSpan.FromMinutes(1);

    extension(IServiceCollection services)
    {
        public IServiceCollection AddCaptcha()
        {
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<ICaptchaService, CaptchaService>();

            return services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddClientPolicy(RateLimitPolicy, ChallengeLimit, ChallengeWindow);
            });
        }
    }

    extension(RouteHandlerBuilder builder)
    {
        public RouteHandlerBuilder RequireCaptcha<T>() where T : class, ICaptchaRequest =>
            builder.AddEndpointFilter<CaptchaFilter<T>>();
    }
}
