namespace Behind.Common.Database.Modules;

public interface IModuleCreationOrder
{
    IOrderedQueryable<TEntity> NewestFirst<TEntity>(IQueryable<TEntity> source) where TEntity : class;
}
