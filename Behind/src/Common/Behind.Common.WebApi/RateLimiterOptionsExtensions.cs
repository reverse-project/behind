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
            options.AddClientPolicy(name, _ => (limit, window));

        public RateLimiterOptions AddClientPolicy(string name, Func<HttpContext, (int Limit, TimeSpan Window)> current) =>
            options.AddPolicy(
                name,
                context =>
                {
                    var (limit, window) = current(context);

                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"{context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient}|{limit}|{window}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = limit,
                            Window = window,
                            QueueLimit = 0
                        });
                });
    }
}
