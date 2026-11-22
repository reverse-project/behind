using Behind.Common.Database;
using Microsoft.Data.Sqlite;
using Npgsql;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.WebApi.Middleware;

public sealed record SubaccountTransactionMetadata;

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
        catch (Exception exception) when (IsConcurrencyFailure(exception)
            || context.GetEndpoint()?.Metadata.GetMetadata<SubaccountTransactionMetadata>() is not null && IsFamilyConflict(exception))
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

    private static bool IsFamilyConflict(Exception exception) =>
        UniqueConstraintViolation.Matches(exception, "PK_Subaccounts", "Subaccounts.UserId")
        || exception is SqliteException { SqliteErrorCode: 5 or 6 }
        || exception is SqliteException { SqliteExtendedErrorCode: 1811 } sqlite
            && sqlite.Message.Contains("Subaccounts allow only one level", StringComparison.Ordinal)
        || exception is PostgresException { SqlState: "P0001", MessageText: "Subaccounts allow only one level" }
        || exception.InnerException is { } inner && IsFamilyConflict(inner);

    private static bool IsConcurrencyFailure(Exception exception) =>
        exception is DbUpdateConcurrencyException
        || exception is System.Data.Common.DbException database && database.SqlState is "40001" or "40P01"
        || exception.InnerException is { } inner && IsConcurrencyFailure(inner);
}
