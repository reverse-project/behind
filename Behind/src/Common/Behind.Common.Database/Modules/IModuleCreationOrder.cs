namespace Behind.Common.Database.Modules;

public interface IModuleCreationOrder
{
    IOrderedQueryable<TEntity> NewestFirst<TEntity>(IQueryable<TEntity> source) where TEntity : class;

    Task<IReadOnlyDictionary<Guid, DateTimeOffset?>> CreatedAtAsync<TEntity>(
        IReadOnlyCollection<Guid> keys, CancellationToken cancellationToken = default) where TEntity : class;
}
