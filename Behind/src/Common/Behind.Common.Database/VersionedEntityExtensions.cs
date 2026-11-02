using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Common.Database;

public static class VersionedColumns
{
    public const string Id = "Id";
    public const string Key = "Key";
    public const string Version = "Version";
    public const long FirstVersion = 1;
}

public static class VersionedEntityExtensions
{
    extension<TEntity>(EntityTypeBuilder<TEntity> builder) where TEntity : class
    {
        public EntityTypeBuilder<TEntity> HasVersionedKey()
        {
            builder.Ignore(VersionedColumns.Id);
            builder.Property<Guid>(VersionedColumns.Key).ValueGeneratedNever();
            builder.Property<long>(VersionedColumns.Version).ValueGeneratedNever();
            builder.HasKey(VersionedColumns.Key, VersionedColumns.Version);

            return builder;
        }
    }

    extension<TEntity>(IQueryable<TEntity> source) where TEntity : class
    {
        public IQueryable<TEntity> OfKey(Guid key) =>
            source.Where(entity => EF.Property<Guid>(entity, VersionedColumns.Key) == key);

        public IQueryable<TEntity> OfKeys(IReadOnlyCollection<Guid> keys) =>
            source.Where(entity => keys.Contains(EF.Property<Guid>(entity, VersionedColumns.Key)));

        public IQueryable<TEntity> OfVersion(VersionedKey<Guid> id) =>
            source.OfKey(id.Key).Where(entity => EF.Property<long>(entity, VersionedColumns.Version) == id.Version);

        public IQueryable<TEntity> LatestOf(IQueryable<TEntity> all) =>
            source.Where(entity => EF.Property<long>(entity, VersionedColumns.Version)
                                   == all.Where(other => EF.Property<Guid>(other, VersionedColumns.Key)
                                                         == EF.Property<Guid>(entity, VersionedColumns.Key))
                                       .Max(other => EF.Property<long>(other, VersionedColumns.Version)));

        public async Task<long> LatestVersionAsync(Guid key, CancellationToken cancellationToken = default) =>
            await source.OfKey(key)
                .MaxAsync(entity => (long?)EF.Property<long>(entity, VersionedColumns.Version), cancellationToken)
            ?? 0;
    }

    extension(EntityEntry entry)
    {
        public void StampVersionedKey(Guid key, long version)
        {
            entry.Property(VersionedColumns.Key).CurrentValue = key;
            entry.Property(VersionedColumns.Version).CurrentValue = version;
        }

        public VersionedKey<Guid> VersionedId =>
            new(
                (Guid)entry.Property(VersionedColumns.Key).CurrentValue!,
                (long)entry.Property(VersionedColumns.Version).CurrentValue!);
    }

    extension(DbContext context)
    {
        public async Task<int> MarkSoftDeletedAsync<TEntity>(
            IQueryable<TEntity> rows,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            var found = await rows.ToListAsync(cancellationToken);

            context.RemoveRange(found);

            return found.Count;
        }

        public async Task<int> MarkRestoredAsync<TEntity>(
            IQueryable<TEntity> rows,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            var deleted = await context.DeletedRowsAsync(rows, cancellationToken);

            deleted.ForEach(row => context.SoftDeleteContext.Restores.Mark(row));

            return deleted.Count;
        }

        public async Task<int> MarkHardDeletedAsync<TEntity>(
            IQueryable<TEntity> rows,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            var deleted = await context.DeletedRowsAsync(rows, cancellationToken);

            deleted.ForEach(context.HardDelete);

            return deleted.Count;
        }

        public async Task<bool> HasDeletedRowsAsync<TEntity>(
            IQueryable<TEntity> rows,
            CancellationToken cancellationToken = default)
            where TEntity : class =>
            (await context.DeletedRowsAsync(rows, cancellationToken)).Count > 0;

        private async Task<List<TEntity>> DeletedRowsAsync<TEntity>(
            IQueryable<TEntity> rows,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            List<TEntity> found;

            using (context.SoftDeleteContext.SoftDelete.IncludeDeleted())
            {
                found = await rows.ToListAsync(cancellationToken);
            }

            var deleted = new List<TEntity>();

            foreach (var row in found)
            {
                if (await context.Entry(row).IsDeletedAsync(cancellationToken))
                {
                    deleted.Add(row);
                }
            }

            return deleted;
        }
    }
}
