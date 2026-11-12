using Microsoft.EntityFrameworkCore;

namespace Behind.Common.Database;

public static class ContentChildExtensions
{
    extension(DbContext context)
    {
        public async Task<bool> HasContentChildren<TEntity>(
            Guid ownerId, string ownerProperty, IEnumerable<Guid?> ids, CancellationToken cancellationToken)
            where TEntity : class
        {
            var wanted = ids.OfType<Guid>().ToArray();
            if (wanted.Contains(Guid.Empty) || wanted.Distinct().Count() != wanted.Length)
            {
                return false;
            }
            var found = await context.Set<TEntity>().IgnoreQueryFilters([ContentLocaleExtensions.FilterName])
                .Where(row => EF.Property<Guid>(row, ownerProperty) == ownerId
                    && wanted.Contains(EF.Property<Guid>(row, VersionedColumns.Key)))
                .Select(row => EF.Property<Guid>(row, VersionedColumns.Key)).Distinct().CountAsync(cancellationToken);
            return found == wanted.Length;
        }
    }
}
