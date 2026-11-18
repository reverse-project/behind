using Behind.Common;
using Behind.Common.Logging;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Middleware;

public sealed class DeletedRecordsVisibilityMiddleware(
    RequestDelegate next,
    ILogger<DeletedRecordsVisibilityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AbstractCoreContext database, ISessionQueries sessions, IAccessQueries accessQueries)
    {
        var session = await context.FindCurrentSession(sessions);
        var canViewDeleted = await session.Match(
            () => Task.FromResult(false),
            async current => (await accessQueries.GetUserAccess(current.UserId, context.RequestAborted)).Holds(DomainConstants.ViewDeletedGrant));

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
