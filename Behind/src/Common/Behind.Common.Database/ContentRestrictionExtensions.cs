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
            if (blocked.Count == 0)
            {
                return [];
            }
            var parentProperties = (string[])context.Model.FindAnnotation("Behind:AgeRestrictionParents")!.Value!;
            var links = context.Model.GetEntityTypes()
                .Where(type => type.FindAnnotation("Behind:AgeRestricted") is not null)
                .SelectMany(type => type.GetProperties()
                    .Where(property => (parentProperties.Contains(property.Name) || MediaProperties.Contains(property.Name))
                                       && (property.ClrType == typeof(Guid) || property.ClrType == typeof(Guid?)))
                    .Select(property => (Type: type.ClrType, Property: property.Name, Nullable: property.ClrType == typeof(Guid?))))
                .ToArray();
            var frontier = blocked.ToArray();
            while (frontier.Length > 0)
            {
                var next = new List<Guid>();
                foreach (var link in links)
                {
                    var method = typeof(ContentRestrictionExtensions)
                        .GetMethod(nameof(ReachableIds), BindingFlags.Static | BindingFlags.NonPublic)!
                        .MakeGenericMethod(link.Type);
                    var reachable = await (Task<List<Guid>>)method.Invoke(null,
                        [context, link.Property, link.Nullable, frontier, cancellationToken])!;
                    next.AddRange(reachable.Where(blocked.Add));
                }

                frontier = next.ToArray();
            }

            return blocked.ToArray();
        }
    }

    private static Task<List<Guid>> ReachableIds<TEntity>(
        DbContext context, string property, bool nullable, Guid[] frontier, CancellationToken cancellationToken)
        where TEntity : class
    {
        var rows = context.Set<TEntity>().AsNoTracking()
            .IgnoreQueryFilters([FilterName, SoftDeleteExtensions.FilterName]);
        var linked = nullable
            ? rows.Where(row => EF.Property<Guid?>(row, property).HasValue)
            : rows.Where(row => EF.Property<Guid>(row, property) != Guid.Empty);
        var edges = linked.Select(row => new
        {
            Owner = EF.Property<Guid>(row, VersionedColumns.Key),
            Reference = nullable ? EF.Property<Guid?>(row, property)!.Value : EF.Property<Guid>(row, property)
        });
        var children = edges.Where(edge => frontier.Contains(edge.Reference)).Select(edge => edge.Owner);
        if (MediaProperties.Contains(property))
        {
            var media = edges.Where(edge => frontier.Contains(edge.Owner)).Select(edge => edge.Reference);
            return children.Union(media).ToListAsync(cancellationToken);
        }

        return children.Distinct().ToListAsync(cancellationToken);
    }
}
