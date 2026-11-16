using Microsoft.EntityFrameworkCore;

namespace Behind.Common.Database.Modules;

public interface IModuleContext : ISoftDeleteContext
{
    DbContext Context { get; }

    IQueryable<TEntity> InDeletionState<TEntity>(IQueryable<TEntity> source, DeletionState state)
        where TEntity : class;
}
