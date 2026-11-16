using Microsoft.AspNetCore.Builder;

namespace Behind.Common.WebApi.Middleware;

public static class RequestLoggingMiddlewareExtensions
{
    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseRequestLogging() => app.UseMiddleware<RequestLoggingMiddleware>();
    }
}
