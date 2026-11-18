using System.Data;
using Behind.Common.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.WebApi.Middleware;

public sealed class AuthenticationTransactionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork)
    {
        var path = context.Request.Path;
        var protectedRequest = !HttpMethods.IsGet(context.Request.Method)
            && (path.StartsWithSegments("/sign/in")
                || path.StartsWithSegments("/session/elevate")
                || path.Equals(new PathString("/profile/password"))
                || path.StartsWithSegments("/profile/two-factor"));

        if (!protectedRequest || unitOfWork is not DbContext database || !database.Database.IsRelational())
        {
            await next(context);
            return;
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, context.RequestAborted);
        var response = context.Response.Body;
        await using var buffered = new MemoryStream();
        context.Response.Body = buffered;
        try
        {
            await next(context);
            if (context.Response.StatusCode < 400)
            {
                await transaction.CommitAsync(context.RequestAborted);
            }
            else
            {
                await transaction.RollbackAsync(context.RequestAborted);
            }
            buffered.Position = 0;
            await buffered.CopyToAsync(response, context.RequestAborted);
        }
        catch (Exception exception) when (IsConcurrencyFailure(exception))
        {
            await transaction.RollbackAsync(context.RequestAborted);
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status409Conflict;
        }
        finally
        {
            context.Response.Body = response;
        }
    }

    private static bool IsConcurrencyFailure(Exception exception) =>
        exception is DbUpdateConcurrencyException
        || exception is System.Data.Common.DbException database && database.SqlState is "40001" or "40P01"
        || exception.InnerException is { } inner && IsConcurrencyFailure(inner);
}
