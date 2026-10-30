using static Behind.Common.FunctionalExtensions;
using Behind.Common;
using Behind.Core.Database;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Middleware;

public sealed class SessionValidationMiddleware(RequestDelegate next, TimeProvider timeProvider)
{
    public async Task InvokeAsync(HttpContext context, ISessionQueries sessionQueries)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var rejection = await FindRejectionAsync(context, sessionQueries);

        await rejection.Match(() => next(context), context.Reject);
    }

    private async Task<Option<SessionRejection>> FindRejectionAsync(
        HttpContext context,
        ISessionQueries sessionQueries) =>
        await context.User.SessionClaims.Match(
            () => Task.FromResult(Some(SessionRejection.MissingSession)),
            async claims =>
            {
                var session = await sessionQueries.TryFindById(claims.SessionId, context.RequestAborted);
                return session.Match(
                    () => Some(SessionRejection.UnknownSession),
                    found => found.RejectionFor(claims.Nonce, timeProvider.GetUtcNow()));
            });
}
