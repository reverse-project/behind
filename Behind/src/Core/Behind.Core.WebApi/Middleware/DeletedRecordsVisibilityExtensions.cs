using System.Security.Claims;
using Behind.Common;
using Microsoft.AspNetCore.Builder;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Middleware;

public static class DeletedRecordsVisibilityExtensions
{
    extension(ClaimsPrincipal principal)
    {
        public Option<string> Email =>
            principal.Identity?.IsAuthenticated == true && principal.FindFirstValue(ClaimTypes.Email) is { } email
                ? Some(email)
                : None;
    }

    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseDeletedRecordsVisibility() =>
            app.UseMiddleware<DeletedRecordsVisibilityMiddleware>();
    }
}
