using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class ContentLocaleQueryExtensions
{
    extension(AbstractCoreContext context)
    {
        public Task<string?> FindContentLocale(string entityType, Guid entityId, CancellationToken cancellationToken = default) =>
            context.ContentLocales.LatestOf(context.ContentLocales)
                .Where(item => item.EntityType == entityType && item.EntityId == entityId)
                .Select(item => item.Locale).SingleOrDefaultAsync(cancellationToken);

        public async Task StageContentLocale(
            string entityType, Guid entityId, string? locale, CancellationToken cancellationToken = default)
        {
            using var scope = context.SoftDelete.IncludeDeleted();
            var previous = await context.ContentLocales
                .Where(item => item.EntityType == entityType && item.EntityId == entityId)
                .OrderByDescending(item => EF.Property<long>(item, VersionedColumns.Version))
                .Select(item => new
                {
                    Key = EF.Property<Guid>(item, VersionedColumns.Key),
                    Version = EF.Property<long>(item, VersionedColumns.Version),
                    item.Locale
                }).FirstOrDefaultAsync(cancellationToken);
            var deleted = previous is not null && await context.IsDeletedAsync(nameof(ContentLocale),
                previous.Key.ToString() + ":" + previous.Version, cancellationToken);
            if (previous is not null && previous.Locale == locale && !deleted)
            {
                return;
            }

            var key = previous?.Key ?? Guid.CreateVersion7();
            var version = (previous?.Version ?? 0) + 1;
            var assignment = new ContentLocale(new(key, version), entityType, entityId, locale);
            context.ContentLocales.Add(assignment);
            context.Entry(assignment).StampVersionedKey(key, version);
        }

        public IQueryable<TEntity> InContentLocale<TEntity>(
            IQueryable<TEntity> source, string entityType, string? locale) where TEntity : class
        {
            var assignments = context.ContentLocales.LatestOf(context.ContentLocales)
                .Where(item => item.EntityType == entityType);
            var keyProperty = context.ContentKeyProperty(typeof(TEntity));
            return source.IgnoreQueryFilters([ContentLocaleExtensions.FilterName])
                .Where(row => assignments.Where(item => item.EntityId == EF.Property<Guid>(row, keyProperty))
                    .Select(item => item.Locale).FirstOrDefault() == locale
                    || locale != null && assignments.Where(item => item.EntityId == EF.Property<Guid>(row, keyProperty))
                        .Select(item => item.Locale).FirstOrDefault() == null);
        }
    }
}
