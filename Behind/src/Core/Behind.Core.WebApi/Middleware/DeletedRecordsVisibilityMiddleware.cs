using Behind.Common;
using Behind.Common.Logging;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Middleware;

public sealed class DeletedRecordsVisibilityMiddleware(
    RequestDelegate next,
    ILogger<DeletedRecordsVisibilityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AbstractCoreContext database, IUserQueries userQueries)
    {
        var canViewDeleted = await context.User.Email.Match(
            () => Task.FromResult(false),
            email => userQueries.HasGrant(email, DomainConstants.ViewDeletedGrant, context.RequestAborted));

        if (canViewDeleted)
        {
            logger.LogDebug("Including deleted records for request to {Path}", () => [context.Request.Path.Value]);

            using var scope = database.SoftDelete.IncludeDeleted();
            await next(context);
            return;
        }

        await next(context);
    }
}
