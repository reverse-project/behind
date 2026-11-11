using Behind.Common.Database.Modules;

namespace Behind.Core.Database;

public sealed class ModuleCreationOrder(AbstractCoreContext context) : IModuleCreationOrder
{
    public IOrderedQueryable<TEntity> NewestFirst<TEntity>(IQueryable<TEntity> source) where TEntity : class =>
        context.OrderByCreation(source, newestFirst: true);
}
