using System.Linq.Expressions;
using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database;

public static class AuditTimeExtensions
{
    extension(AbstractCoreContext context)
    {
        public async Task<IReadOnlyDictionary<Guid, AuditTimes>> AuditTimesAsync<TEntity>(
            IReadOnlyDictionary<Guid, long> shownVersions,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            var entityName = typeof(TEntity).Name;

            var keys = shownVersions
                .SelectMany(shown => new[]
                {
                    VersionKey(shown.Key, VersionedColumns.FirstVersion),
                    VersionKey(shown.Key, shown.Value)
                })
                .Distinct()
                .ToList();

            var created = (await context.CreationEntriesAsync(entityName, keys, cancellationToken));

            return shownVersions.ToDictionary(
                shown => shown.Key,
                shown => new AuditTimes(
                    created.TryGetValue(VersionKey(shown.Key, VersionedColumns.FirstVersion), out var first)
                        ? Some(first)
                        : None,
                    shown.Value > VersionedColumns.FirstVersion
                    && created.TryGetValue(VersionKey(shown.Key, shown.Value), out var latest)
                        ? Some(latest)
                        : None));
        }

        public async Task<IReadOnlyDictionary<long, DateTimeOffset>> VersionCreationTimesAsync<TEntity>(
            Guid key,
            IReadOnlyCollection<long> versions,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            var keys = versions.Select(version => VersionKey(key, version)).ToList();
            var created = await context.CreationEntriesAsync(typeof(TEntity).Name, keys, cancellationToken);

            return versions
                .Where(version => created.ContainsKey(VersionKey(key, version)))
                .ToDictionary(version => version, version => created[VersionKey(key, version)]);
        }

        public IOrderedQueryable<TEntity> OrderByCreation<TEntity>(
            IQueryable<TEntity> source,
            bool newestFirst,
            Expression<Func<TEntity, DateTimeOffset?>>? overriddenBy = null)
            where TEntity : class
        {
            var creation = context.CreationTime(overriddenBy);
            var ordered = newestFirst ? source.OrderByDescending(creation) : source.OrderBy(creation);

            return ordered.ThenByKey(newestFirst);
        }

        public IOrderedQueryable<TEntity> ThenByCreation<TEntity>(
            IOrderedQueryable<TEntity> source,
            bool newestFirst,
            Expression<Func<TEntity, DateTimeOffset?>>? overriddenBy = null)
            where TEntity : class
        {
            var creation = context.CreationTime(overriddenBy);
            var ordered = newestFirst ? source.ThenByDescending(creation) : source.ThenBy(creation);

            return ordered.ThenByKey(newestFirst);
        }

        private async Task<IReadOnlyDictionary<string, DateTimeOffset>> CreationEntriesAsync(
            string entityName,
            IReadOnlyCollection<string> keys,
            CancellationToken cancellationToken)
        {
            var entries = await context.AuditLogEntries
                .Where(entry => entry.EntityName == entityName && keys.Contains(entry.EntityKey))
                .Select(entry => new { entry.EntityKey, entry.Action, entry.Timestamp })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return entries
                .Where(entry => entry.Action is AuditLogEntry.Created)
                .GroupBy(entry => entry.EntityKey)
                .ToDictionary(group => group.Key, group => group.Min(entry => entry.Timestamp));
        }

        private Expression<Func<TEntity, DateTimeOffset?>> CreationTime<TEntity>(
            Expression<Func<TEntity, DateTimeOffset?>>? overriddenBy)
            where TEntity : class
        {
            var creations = context.AuditLogEntries
                .Where(entry => entry.EntityName == typeof(TEntity).Name && entry.Action == new AuditLogEntry.Created());

            Expression<Func<string, DateTimeOffset?>> createdAt = key =>
                creations
                    .Where(entry => entry.EntityKey == key)
                    .Select(entry => (DateTimeOffset?)entry.Timestamp)
                    .Max();

            var entity = Expression.Parameter(typeof(TEntity), "entity");

            var creation = ReplacingExpressionVisitor.Replace(
                createdAt.Parameters[0],
                FirstVersionKeyText(entity),
                createdAt.Body);

            return Expression.Lambda<Func<TEntity, DateTimeOffset?>>(
                overriddenBy is null
                    ? creation
                    : Expression.Coalesce(
                        ReplacingExpressionVisitor.Replace(overriddenBy.Parameters[0], entity, overriddenBy.Body),
                        creation),
                entity);
        }
    }

    extension<TEntity>(IOrderedQueryable<TEntity> source) where TEntity : class
    {
        private IOrderedQueryable<TEntity> ThenByKey(bool descending) =>
            descending
                ? source.ThenByDescending(entity => EF.Property<Guid>(entity, VersionedColumns.Key))
                : source.ThenBy(entity => EF.Property<Guid>(entity, VersionedColumns.Key));
    }

    private static string VersionKey(Guid key, long version) => $"{key.ToString().ToLowerInvariant()}:{version}";

    private static Expression FirstVersionKeyText(Expression entity) =>
        Expression.Call(
            typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!,
            Expression.Call(
                Expression.Call(
                    Expression.Call(
                        typeof(EF),
                        nameof(EF.Property),
                        [typeof(Guid)],
                        entity,
                        Expression.Constant(VersionedColumns.Key)),
                    nameof(ToString),
                    Type.EmptyTypes),
                nameof(string.ToLower),
                Type.EmptyTypes),
            Expression.Constant($":{VersionedColumns.FirstVersion}"));
}
