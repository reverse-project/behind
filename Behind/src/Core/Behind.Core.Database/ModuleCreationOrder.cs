using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Modules;

namespace Behind.Core.Database;

public sealed class ModuleCreationOrder(AbstractCoreContext context) : IModuleCreationOrder
{
    public IOrderedQueryable<TEntity> NewestFirst<TEntity>(IQueryable<TEntity> source) where TEntity : class =>
        context.OrderByCreation(source, newestFirst: true);

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset?>> CreatedAtAsync<TEntity>(
        IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken = default) where TEntity : class
    {
        var versions = keys.Distinct().ToDictionary(key => key, _ => VersionedColumns.FirstVersion);
        var times = await context.AuditTimesAsync<TEntity>(versions, cancellationToken);
        return times.ToDictionary(pair => pair.Key, pair => pair.Value.CreatedAt.ToNullable());
    }
}
