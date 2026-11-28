using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Behind.Core.Database.Auditing;

public sealed class AuditSaveChangesInterceptor(Func<IAuditActorProvider> actorProviderFactory, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is { } context)
        {
            var actor = await actorProviderFactory().GetCurrentAsync(cancellationToken);
            await context.AddAuditEntriesAsync(actor, timeProvider.GetUtcNow(), cancellationToken);
            context.CancelSoftDeletes();
            foreach (var entry in context.ChangeTracker.Entries<ServiceUser>()
                         .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                entry.State = context is AbstractCoreContext auditContext
                    && auditContext.HardDeletes.IsMarked(entry.Entity)
                    ? EntityState.Detached
                    : EntityState.Unchanged;
            }
            if (context is AbstractCoreContext coreContext)
            {
                await coreContext.LockAuditReferencesAsync(cancellationToken);
            }
        }

        return result;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    ) =>
        throw new NotSupportedException("Audited changes must be saved with SaveChangesAsync.");
}
