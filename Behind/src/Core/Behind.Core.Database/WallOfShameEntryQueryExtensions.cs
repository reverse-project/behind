using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class WallOfShameEntryQueryExtensions
{
    extension(IQueryable<WallOfShameEntry> entries)
    {
        public IQueryable<WallOfShameEntry> ActiveAt(DateTimeOffset now) =>
            entries.Where(entry => EF.Property<DateTimeOffset?>(entry, "ExpiresAt") == null
                                   || EF.Property<DateTimeOffset?>(entry, "ExpiresAt") > now);

        public IQueryable<WallOfShameEntry> ForUser(Guid userId) =>
            entries.Where(entry => EF.Property<Guid>(entry, "UserId") == userId);
    }
}
