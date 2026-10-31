using Behind.Common.Database.Implementation;
using Microsoft.EntityFrameworkCore;

namespace Behind.Common.Database;

public static class DbContextExtensions
{
    public static IImmutableEntityRepository<TEntity> ToImmutableEntityRepository<TEntity>(
        this DbSet<TEntity> dbSet, DbContext dbContext, params string[] includes)
        where TEntity : class =>
        new ImmutableEntityRepository<TEntity>(dbContext, dbSet, includes);

    extension(DbContext dbContext)
    {
        public void UpdateValuesByKey<TEntity>(object[] keyValues, TEntity modified) where TEntity : class
        {
            var original = dbContext.Set<TEntity>().Find(keyValues)
                           ?? throw new InvalidOperationException("Original entity must exist.");

            dbContext.Entry(original).CurrentValues.SetValues(modified);
        }

        public async Task DeleteByKeyAsync<TEntity>(
            object[] keyValues,
            CancellationToken cancellationToken = default) where TEntity : class
        {
            var entity = await dbContext.Set<TEntity>().FindAsync(keyValues, cancellationToken);

            if (entity is not null)
            {
                dbContext.Remove(entity);
            }
        }
    }
}
