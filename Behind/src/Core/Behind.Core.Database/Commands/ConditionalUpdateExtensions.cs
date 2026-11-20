using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;

namespace Behind.Core.Database.Commands;

internal static class ConditionalUpdateExtensions
{
    extension(AbstractCoreContext context)
    {
        public async Task<bool> UpdateMatchingRowAsync<TEntity>(
            IQueryable<TEntity> matching,
            Action<UpdateSettersBuilder<TEntity>> lockRow,
            Action<EntityEntry<TEntity>> update,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            await using var transaction = context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancellationToken)
                : null;

            if (await matching.ExecuteUpdateAsync(lockRow, cancellationToken) != 1)
            {
                return false;
            }

            var entry = context.Entry(await matching.AsTracking().SingleAsync(cancellationToken));
            await entry.ReloadAsync(cancellationToken);
            update(entry);
            await context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return true;
        }
    }
}
