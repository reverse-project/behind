using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Behind.Common.WebApi;

public static class RateLimiterOptionsExtensions
{
    private const string UnknownClient = "unknown";

    extension(RateLimiterOptions options)
    {
        public RateLimiterOptions AddClientPolicy(string name, int limit, TimeSpan window) =>
            options.AddPolicy(
                name,
                context => RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit,
                        Window = window,
                        QueueLimit = 0
                    }));
    }
}
