using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Behind.Common.WebApi.Middleware;

public static class CorrelationIdMiddlewareExtensions
{
    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseCorrelationId(CorrelationIdOptions? options = null) =>
            app.UseMiddleware<CorrelationIdMiddleware>(options ?? CorrelationIdOptions.Default);
    }

    extension(HttpContext context)
    {
        public string? CorrelationId => context.Features.Get<CorrelationIdFeature>()?.CorrelationId;
    }
}
