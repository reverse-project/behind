using Microsoft.AspNetCore.Builder;

namespace Behind.Common.WebApi.Middleware;

public static class ETagMiddlewareExtensions
{
    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseETag(ETagOptions? options = null) =>
            app.UseMiddleware<ETagMiddleware>(options ?? ETagOptions.Default);
    }
}
