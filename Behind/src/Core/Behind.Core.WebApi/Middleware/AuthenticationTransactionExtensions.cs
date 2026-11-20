using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Middleware;

public static class AuthenticationTransactionExtensions
{
    extension(RouteHandlerBuilder builder)
    {
        public RouteHandlerBuilder RequireAuthenticationTransaction(int concurrencyFailureStatusCode = StatusCodes.Status409Conflict) =>
            builder.WithMetadata(new AuthenticationTransactionMetadata(concurrencyFailureStatusCode))
                .AddEndpointFilter<AuthenticationTransactionFilter>();
    }
}

public sealed class AuthenticationTransactionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var transaction = context.HttpContext.Features.Get<AuthenticationTransaction>()
            ?? throw new InvalidOperationException("Authentication transaction middleware is required.");
        await transaction.BeginAsync(context.HttpContext.RequestAborted);
        return await next(context);
    }
}
