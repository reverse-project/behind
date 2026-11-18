using System.Data;
using Behind.Core.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.WebApi.Endpoints;

public sealed class TwoFactorMutationFilter(AbstractCoreContext database) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, context.HttpContext.RequestAborted);
        var result = await next(context);
        if (result is IStatusCodeHttpResult { StatusCode: >= 400 })
        {
            await transaction.RollbackAsync(context.HttpContext.RequestAborted);
        }
        else
        {
            await transaction.CommitAsync(context.HttpContext.RequestAborted);
        }

        return result;
    }
}
