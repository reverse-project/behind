using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Behind.Core.WebApi.Middleware;

public sealed record AuthenticationTransactionMetadata(int ConcurrencyFailureStatusCode = 409);

public sealed class AuthenticationTransaction(DbContext? database) : IAsyncDisposable
{
    private IDbContextTransaction? transaction;
    private readonly List<Func<Task>> committed = [];
    private readonly List<Action> rolledBack = [];
    private bool completed;
    private bool transactionEnded;

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (transaction is null && database is not null)
        {
            transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        }
    }

    public void OnCommitted(Action action) => OnCommittedAsync(() =>
    {
        action();
        return Task.CompletedTask;
    });

    internal void OnCommittedAsync(Func<Task> action) => committed.Add(action);

    public void OnRolledBack(Action action) => rolledBack.Add(action);

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (transaction is not null)
        {
            try
            {
                await transaction.CommitAsync(cancellationToken);
            }
            catch (System.Data.Common.DbException exception) when (exception.SqlState is "40001" or "40P01")
            {
                transactionEnded = true;
                throw;
            }
        }
        completed = true;
        foreach (var action in committed)
        {
            await action();
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (completed)
        {
            return;
        }
        try
        {
            if (transaction is not null && !transactionEnded)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            completed = true;
            foreach (var action in rolledBack)
            {
                action();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await RollbackAsync();
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}
