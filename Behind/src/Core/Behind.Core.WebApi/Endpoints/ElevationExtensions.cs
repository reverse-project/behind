using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Core.WebApi.Endpoints;

public static class ElevationExtensions
{
    public const string RateLimitPolicy = "elevation";
    public const int AttemptLimit = 5;
    public static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(10);

    private const string UnknownClient = "unknown";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddElevationRateLimiting() =>
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy(
                    RateLimitPolicy,
                    context => RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = AttemptLimit,
                            Window = AttemptWindow,
                            QueueLimit = 0
                        }));
            });
    }
}
