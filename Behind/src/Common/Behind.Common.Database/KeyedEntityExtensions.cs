using Microsoft.EntityFrameworkCore;

namespace Behind.Common.Database;

public sealed record Keyed<TEntity>(Guid Key, long Version, TEntity Entity);

public static class KeyedEntityExtensions
{
    extension<TEntity>(IQueryable<TEntity> source) where TEntity : class
    {
        public IQueryable<Keyed<TEntity>> Keyed() =>
            source.Select(entity => new Keyed<TEntity>(
                EF.Property<Guid>(entity, VersionedColumns.Key),
                EF.Property<long>(entity, VersionedColumns.Version),
                entity));

        public async Task<IReadOnlyList<Keyed<TEntity>>> ToKeyedAsync(CancellationToken cancellationToken = default) =>
            await source.Keyed().AsNoTracking().ToListAsync(cancellationToken);
    }

    extension<TEntity>(IEnumerable<Keyed<TEntity>> rows)
    {
        public IEnumerable<Keyed<TEntity>> Latest() =>
            rows.GroupBy(row => row.Key).Select(versions => versions.MaxBy(row => row.Version)!);
    }
}
