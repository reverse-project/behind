using static Behind.Common.FunctionalExtensions;
using Behind.Common;
using Behind.Common.Logging;
using Behind.Core.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Middleware;

public sealed class SessionValidationMiddleware(
    RequestDelegate next,
    TimeProvider timeProvider,
    ILogger<SessionValidationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ISessionQueries sessionQueries)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            logger.LogDebug(
                "Skipping session validation for anonymous request to {Path}",
                () => [context.Request.Path.Value]);

            await next(context);
            return;
        }

        var rejection = await FindRejectionAsync(context, sessionQueries);

        await rejection.Match(
            () =>
            {
                logger.LogDebug(
                    "Session {SessionId} validated for request to {Path}",
                    () => [SessionIdOf(context), context.Request.Path.Value]);

                return next(context);
            },
            found =>
            {
                logger.LogWarning(
                    "Rejected request to {Path} for session {SessionId} with {StatusCode}: {Reason}",
                    () => [context.Request.Path.Value, SessionIdOf(context), found.StatusCode, found.Reason]);

                return context.Reject(found);
            });
    }

    private static Guid? SessionIdOf(HttpContext context) =>
        context.User.SessionId.Match(() => (Guid?)null, id => id);

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
