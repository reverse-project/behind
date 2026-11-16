using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class WallOfShameEntryQueryExtensions
{
    extension(AbstractCoreContext context)
    {
        public IOrderedQueryable<WallOfShameEntry> NewestBans(IQueryable<WallOfShameEntry> entries) =>
            entries.OrderByDescending(entry => context.AuditLogEntries
                    .Where(audit => audit.EntityName == nameof(WallOfShameEntry)
                                    && audit.EntityKey == EF.Property<Guid>(entry, "Id").ToString().ToLower()
                                    && audit.Action == new AuditLogEntry.Created())
                    .Select(audit => (DateTimeOffset?)audit.Timestamp)
                    .Max())
                .ThenByDescending(entry => EF.Property<Guid>(entry, "Id"));
    }

    extension(IQueryable<WallOfShameEntry> entries)
    {
        public IQueryable<WallOfShameEntry> ActiveAt(DateTimeOffset now) =>
            entries.Where(entry => EF.Property<DateTimeOffset?>(entry, "ExpiresAt") == null
                                   || EF.Property<DateTimeOffset?>(entry, "ExpiresAt") > now);

        public IQueryable<WallOfShameEntry> ForUser(Guid userId) =>
            entries.Where(entry => EF.Property<Guid>(entry, "UserId") == userId);
    }
}
