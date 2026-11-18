using Behind.Common.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.WebApi.Middleware;

public sealed class AuthenticationTransactionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<AuthenticationTransactionMetadata>();
        if (metadata is null)
        {
            await next(context);
            return;
        }

        var database = unitOfWork as DbContext;
        await using var transaction = new AuthenticationTransaction(
            database?.Database.IsRelational() == true ? database : null);
        context.Features.Set(transaction);
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
                await transaction.RollbackAsync(CancellationToken.None);
            }
            buffered.Position = 0;
            await buffered.CopyToAsync(response, context.RequestAborted);
        }
        catch (Exception exception) when (IsConcurrencyFailure(exception))
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackException) when (rollbackException is System.Data.Common.DbException or InvalidOperationException)
            {
            }
            context.Response.Clear();
            context.Response.StatusCode = metadata.ConcurrencyFailureStatusCode;
        }
        finally
        {
            context.Response.Body = response;
            context.Features.Set<AuthenticationTransaction>(null);
        }
    }

    private static bool IsConcurrencyFailure(Exception exception) =>
        exception is DbUpdateConcurrencyException
        || exception is System.Data.Common.DbException database && database.SqlState is "40001" or "40P01"
        || exception.InnerException is { } inner && IsConcurrencyFailure(inner);
}
