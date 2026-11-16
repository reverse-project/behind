using Behind.Common.Database.Implementation;
using Microsoft.EntityFrameworkCore;

namespace Behind.Common.Database;

public static class ActiveVersionExtensions
{
    extension(DbContext context)
    {
        public async Task<IReadOnlyDictionary<string, long>> ActiveVersionsAsync(
            string entityType,
            IReadOnlyCollection<string> entityKeys,
            CancellationToken cancellationToken = default)
        {
            var active = await context.Set<ActiveVersion>()
                .Where(version => version.EntityType == entityType && entityKeys.Contains(version.EntityKey))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return active.ToDictionary(version => version.EntityKey, version => version.Version);
        }

        public async Task SetActiveVersionAsync(
            string entityType,
            string entityKey,
            long version,
            CancellationToken cancellationToken = default)
        {
            var activeVersions = context.Set<ActiveVersion>();
            var current = await activeVersions.FindAsync([entityType, entityKey], cancellationToken);
            var next = new ActiveVersion(entityType, entityKey, version);

            if (current is null)
            {
                await activeVersions.AddAsync(next, cancellationToken);
                return;
            }

            if (current.Version != version)
            {
                activeVersions.ToImmutableEntityRepository(context).UpdateImmutable([entityType, entityKey], next);
            }
        }

        public async Task ClearActiveVersionAsync(
            string entityType,
            string entityKey,
            CancellationToken cancellationToken = default)
        {
            var current = await context.Set<ActiveVersion>().FindAsync([entityType, entityKey], cancellationToken);

            if (current is not null)
            {
                context.Remove(current);
            }
        }
    }
}
