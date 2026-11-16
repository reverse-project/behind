using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public static class CommentsEnabledExtensions
{
    extension(RouteHandlerBuilder builder)
    {
        public RouteHandlerBuilder RequireCommentsOf(string entity) =>
            builder.AddEndpointFilter(async (context, next) =>
                context.HttpContext.RequestServices
                    .GetRequiredService<IOptionsSnapshot<CommentsConfiguration>>()
                    .Value
                    .IsEnabled(entity)
                    ? await next(context)
                    : Results.NotFound());
    }
}
