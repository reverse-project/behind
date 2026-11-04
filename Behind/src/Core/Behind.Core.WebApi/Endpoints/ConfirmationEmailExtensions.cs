using Behind.Common.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Core.WebApi.Endpoints;

public static class ConfirmationEmailExtensions
{
    public const string ResendRateLimitPolicy = "confirmation-resend";
    public const int ResendLimit = 3;
    public static readonly TimeSpan ResendWindow = TimeSpan.FromMinutes(10);

    extension(IServiceCollection services)
    {
        public IServiceCollection AddConfirmationEmailRateLimiting() =>
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddClientPolicy(ResendRateLimitPolicy, ResendLimit, ResendWindow);
            });
    }
}
