using Behind.Common.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Core.WebApi.Endpoints;

public static class ElevationExtensions
{
    public const string RateLimitPolicy = "elevation";
    public const int AttemptLimit = 5;
    public static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(10);

    extension(IServiceCollection services)
    {
        public IServiceCollection AddElevationRateLimiting() =>
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddClientPolicy(RateLimitPolicy, AttemptLimit, AttemptWindow);
            });
    }
}
