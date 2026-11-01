using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Auditing;

public static class DbContextAuditExtensions
{
    extension(DbContext context)
    {
        public async Task AddAuditEntriesAsync(
            AuditActor actor,
            DateTimeOffset timestamp,
            CancellationToken cancellationToken = default
        )
        {
            var entries = context.ChangeTracker.Entries()
                .Where(entry => entry.Entity is not AuditLogEntry)
                .Where(entry =>
                    entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                    || (entry.State == EntityState.Unchanged && entry.IsRestored))
                .ToList();

            foreach (var entry in entries)
            {
                var action = await entry.ToAuditActionAsync(cancellationToken);

                action.Match(
                    () => { },
                    value => context.AddAuditEntry(
                        new AuditLogEntry(entry.AuditEntityName, entry.AuditEntityKey, value, timestamp),
                        value is AuditLogEntry.ImpersonationStarted
                            ? actor.OnBehalfOf(entry.SessionUserId)
                            : actor
                    )
                );
            }
        }

        private void AddAuditEntry(AuditLogEntry auditLogEntry, AuditActor actor)
        {
            var audit = context.Add(auditLogEntry);
            audit.Property<Guid?>("UserId").CurrentValue = actor.UserId.Match(() => (Guid?)null, id => id);
            audit.Property<Guid?>("BehalfOfId").CurrentValue = actor.BehalfOfId.Match(() => (Guid?)null, id => id);
        }
    }
}
