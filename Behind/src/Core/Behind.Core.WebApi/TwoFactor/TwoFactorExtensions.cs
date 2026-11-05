using Behind.Common;
using Behind.Common.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Behind.Core.WebApi.TwoFactor;

public static class TwoFactorExtensions
{
    public const string RateLimitPolicy = "two-factor";
    public const int AttemptLimit = 10;
    public static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(10);

    extension(IServiceCollection services)
    {
        public IServiceCollection AddTwoFactor()
        {
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<ITwoFactorTickets, TwoFactorTickets>();
            services.TryAddSingleton<IPasskeyCeremonies, PasskeyCeremonies>();
            services.TryAddScoped<ITwoFactorGate, TwoFactorGate>();
            services.TryAddScoped<ITwoFactorSignIn, TwoFactorSignIn>();
            services.TryAddScoped<IRecoveryCodes, RecoveryCodes>();
            services.TryAddScoped<ITwoFactorVerification, TwoFactorVerification>();
            services.TryAddScoped<ITwoFactorEnrollment, TwoFactorEnrollment>();

            return services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddClientPolicy(RateLimitPolicy, AttemptLimit, AttemptWindow);
            });
        }
    }

    extension(HttpContext httpContext)
    {
        public Uri PasskeyOrigin(IConfiguration configuration) =>
            configuration.FrontendUri().GetOrElse(() => httpContext.BaseUri());
    }
}
