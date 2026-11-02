using Behind.Common.Database;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class VersionedLiveExtensions
{
    extension<TEntity>(IQueryable<TEntity> source) where TEntity : class
    {
        public IQueryable<TEntity> LiveIn(IQueryable<ActiveVersion> active, string entityType) =>
            source.Where(entity => active.Any(version =>
                version.EntityType == entityType
                && version.EntityKey == EF.Property<Guid>(entity, VersionedColumns.Key).ToString().ToLower()
                && version.Version == EF.Property<long>(entity, VersionedColumns.Version)));
    }
}
