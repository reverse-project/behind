using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Behind.Core.WebApi.Middleware;

public sealed record AuthenticationTransactionMetadata;

public sealed class AuthenticationTransaction(DbContext? database) : IAsyncDisposable
{
    private IDbContextTransaction? transaction;
    private readonly List<Action> committed = [];
    private readonly List<Action> rolledBack = [];
    private bool completed;

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (transaction is null && database is not null)
        {
            transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        }
    }

    public void OnCommitted(Action action) => committed.Add(action);

    public void OnRolledBack(Action action) => rolledBack.Add(action);

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        completed = true;
        foreach (var action in committed)
        {
            action();
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
            if (transaction is not null)
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
