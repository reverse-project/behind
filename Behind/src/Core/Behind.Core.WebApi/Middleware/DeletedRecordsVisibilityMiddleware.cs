using Behind.Common;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Middleware;

public sealed class DeletedRecordsVisibilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AbstractCoreContext database, IUserQueries userQueries)
    {
        var canViewDeleted = await context.User.Email.Match(
            () => Task.FromResult(false),
            email => userQueries.HasGrant(email, DomainConstants.ViewDeletedGrant, context.RequestAborted));

        if (canViewDeleted)
        {
            using var scope = database.SoftDelete.IncludeDeleted();
            await next(context);
            return;
        }

        await next(context);
    }
}
