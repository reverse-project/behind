using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Behind.Common.Database;

public interface IContentRestrictionContext
{
    Guid[] HiddenContentIds { get; set; }
}

public static class ContentRestrictionExtensions
{
    public const string FilterName = "ContentAge";

    private static readonly HashSet<string> MediaProperties =
    [
        "ImageId", "ThumbnailId", "ThumbnailImageId", "HeaderImageId", "FileId", "LogoId", "PictureId"
    ];

    private static readonly HashSet<string> ParentProperties =
    [
        "NewsId", "ArticleId", "GalleryId", "CarouselId", "FolderId", "BoardId", "TopicId",
        "CategoryId", "TeamId", "PlayerId", "SeasonId", "DisciplineId", "PlayerSeasonId",
        "StatTypeId", "SiteId", "ParentId", "PositionId", "SubjectId"
    ];

    extension(ModelBuilder builder)
    {
        public void ApplyContentRestrictions<TContext>(
            TContext context,
            IEnumerable<Type> types,
            IEnumerable<string>? additionalParentProperties = null)
            where TContext : DbContext, IContentRestrictionContext
        {
            builder.Model.SetAnnotation("Behind:AgeRestrictionParents",
                ParentProperties.Union(additionalParentProperties ?? []).ToArray());
            foreach (var type in types.Distinct())
            {
                var entity = builder.Model.FindEntityType(type);

                if (entity?.FindProperty(VersionedColumns.Key)?.ClrType != typeof(Guid))
                {
                    continue;
                }

                var row = Expression.Parameter(type, "content");
                var key = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(Guid)], row, Expression.Constant(VersionedColumns.Key));
                Expression<Func<bool>> enabled = () => context.HiddenContentIds.Length == 0;
                Expression<Func<Guid, bool>> visible = id => !context.HiddenContentIds.Contains(id);
                var body = Microsoft.EntityFrameworkCore.Query.ReplacingExpressionVisitor.Replace(
                    visible.Parameters[0], key, visible.Body);
                builder.Entity(type).HasQueryFilter(FilterName, Expression.Lambda(Expression.OrElse(enabled.Body, body), row));
                builder.Entity(type).HasAnnotation("Behind:AgeRestricted", true);
            }
        }
    }

    extension(DbContext context)
    {
        public IReadOnlyList<Type> RestrictedContentTypes =>
            context.Model.GetEntityTypes()
                .Where(type => type.FindAnnotation("Behind:AgeRestricted") is not null)
                .Select(type => type.ClrType).ToArray();

        public async Task<Guid[]> InheritContentRestrictions(
            IEnumerable<Guid> hidden,
            CancellationToken cancellationToken = default)
        {
            var blocked = hidden.ToHashSet();
            var parentProperties = (string[])context.Model.FindAnnotation("Behind:AgeRestrictionParents")!.Value!;
            var links = context.Model.GetEntityTypes()
                .Where(type => type.FindAnnotation("Behind:AgeRestricted") is not null)
                .SelectMany(type => type.GetProperties()
                    .Where(property => (parentProperties.Contains(property.Name) || MediaProperties.Contains(property.Name))
                                       && (property.ClrType == typeof(Guid) || property.ClrType == typeof(Guid?)))
                    .Select(property => (Type: type.ClrType, Property: property.Name, Nullable: property.ClrType == typeof(Guid?))))
                .ToArray();
            var previous = -1;

            while (blocked.Count > 0 && blocked.Count != previous)
            {
                previous = blocked.Count;
                var keys = blocked.ToArray();

                foreach (var link in links)
                {
                    var method = typeof(ContentRestrictionExtensions)
                        .GetMethod(nameof(Children), BindingFlags.Static | BindingFlags.NonPublic)!
                        .MakeGenericMethod(link.Type);
                    var children = await (Task<List<Guid>>)method.Invoke(null,
                        [context, link.Property, link.Nullable, keys, cancellationToken])!;
                    blocked.UnionWith(children);
                    if (MediaProperties.Contains(link.Property))
                    {
                        var mediaMethod = typeof(ContentRestrictionExtensions)
                            .GetMethod(nameof(Media), BindingFlags.Static | BindingFlags.NonPublic)!
                            .MakeGenericMethod(link.Type);
                        var media = await (Task<List<Guid>>)mediaMethod.Invoke(null,
                            [context, link.Property, link.Nullable, keys, cancellationToken])!;
                        blocked.UnionWith(media);
                    }
                }
            }

            return blocked.ToArray();
        }
    }

    private static Task<List<Guid>> Children<TEntity>(
        DbContext context, string property, bool nullable, Guid[] parents, CancellationToken cancellationToken)
        where TEntity : class
    {
        var query = context.Set<TEntity>().AsNoTracking();
        query = nullable
            ? query.Where(row => EF.Property<Guid?>(row, property).HasValue
                                 && parents.Contains(EF.Property<Guid?>(row, property)!.Value))
            : query.Where(row => parents.Contains(EF.Property<Guid>(row, property)));
        return query.Select(row => EF.Property<Guid>(row, VersionedColumns.Key)).Distinct().ToListAsync(cancellationToken);
    }

    private static Task<List<Guid>> Media<TEntity>(
        DbContext context, string property, bool nullable, Guid[] owners, CancellationToken cancellationToken)
        where TEntity : class
    {
        var query = context.Set<TEntity>().AsNoTracking()
            .Where(row => owners.Contains(EF.Property<Guid>(row, VersionedColumns.Key)));
        return nullable
            ? query.Where(row => EF.Property<Guid?>(row, property).HasValue)
                .Select(row => EF.Property<Guid?>(row, property)!.Value).Distinct().ToListAsync(cancellationToken)
            : query.Select(row => EF.Property<Guid>(row, property)).Distinct().ToListAsync(cancellationToken);
    }
}
