using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Behind.Common.Database;

public interface IContentLocaleContext
{
    bool IncludeAllContentLocales { get; set; }
    string[] HiddenLocaleContentKeys { get; set; }
}

public sealed record ContentLocaleItem(string EntityType, Guid EntityId, string? Locale);
public sealed record ContentLocaleScope(
    string[] HiddenKeys, IReadOnlyDictionary<string, string?> EffectiveLocales);

public static class ContentLocaleExtensions
{
    public const string FilterName = "ContentLocale";
    private sealed record Node(string Type, Guid Id)
    {
        public string Key => Type + ":" + Id.ToString();
    }
    private sealed record Link(Node Child, Node Parent);
    private sealed record RawLink(Guid Child, Guid Parent, string? OwnerType);

    extension(ModelBuilder builder)
    {
        public void ApplyContentLocales<TContext>(TContext context, IEnumerable<Type> types)
            where TContext : DbContext, IContentLocaleContext
        {
            foreach (var type in types.Distinct())
            {
                var entity = builder.Model.FindEntityType(type);
                var keyProperty = entity?.FindPrimaryKey()?.Properties.SingleOrDefault(property => property.ClrType == typeof(Guid))?.Name;
                if (keyProperty is null)
                {
                    continue;
                }

                var row = Expression.Parameter(type, "content");
                var key = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(Guid)], row,
                    Expression.Constant(keyProperty));
                Expression<Func<Guid, bool>> visible = id =>
                    context.IncludeAllContentLocales || !context.HiddenLocaleContentKeys.Contains(type.Name + ":" + id.ToString().ToLower());
                var body = Microsoft.EntityFrameworkCore.Query.ReplacingExpressionVisitor.Replace(
                    visible.Parameters[0], key, visible.Body);
                builder.Entity(type).HasQueryFilter(FilterName, Expression.Lambda(body, row));
                builder.Entity(type).HasAnnotation("Behind:ContentLocale", true);
            }
        }
    }

    extension(DbContext context)
    {
        public IReadOnlyList<Type> LocalizedContentTypes => context.Model.GetEntityTypes()
            .Where(type => type.FindAnnotation("Behind:ContentLocale") is not null)
            .Select(type => type.ClrType).ToArray();

        public string ContentKeyProperty(Type type) => context.Model.FindEntityType(type)!.FindPrimaryKey()!.Properties
            .Single(property => property.ClrType == typeof(Guid)).Name;

        public async Task<bool> ContentItemExists(Type type, Guid id, CancellationToken cancellationToken = default)
        {
            var method = typeof(ContentLocaleExtensions).GetMethod(nameof(Exists),
                BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(type);
            return await (Task<bool>)method.Invoke(null, [context, id, cancellationToken])!;
        }

        public async Task<bool> HasContentLocaleSlugConflict(
            Type type, Guid id, string? locale, CancellationToken cancellationToken = default)
        {
            if (context is not IContentLocaleStore || context.Model.FindEntityType(type)?.FindProperty("Slug")?.ClrType != typeof(string))
            {
                return false;
            }
            return await Invoke<bool>(nameof(SlugConflict), type, [context, id, locale!, cancellationToken]);
        }

        public async Task<string[]> HiddenForContentLocale(
            IReadOnlyList<ContentLocaleItem> assignments, string? locale, CancellationToken cancellationToken = default) =>
            (await context.ResolveContentLocales(assignments, locale, cancellationToken)).HiddenKeys;

        public async Task<ContentLocaleScope> ResolveContentLocales(
            IReadOnlyList<ContentLocaleItem> assignments,
            string? locale,
            CancellationToken cancellationToken = default)
        {
            var items = new Dictionary<Node, string?>();
            var links = new List<Link>();
            var pending = new List<(string Type, string Property, bool Media, RawLink Row)>();
            var parentNames = ((string[]?)context.Model.FindAnnotation("Behind:AgeRestrictionParents")?.Value ?? [])
                .Concat(["MenuId", "MessageId", "ReplyToId"]).ToHashSet(StringComparer.Ordinal);
            var mediaNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "ImageId", "ThumbnailId", "ThumbnailImageId", "HeaderImageId", "FileId", "LogoId", "PictureId"
            };
            var explicitIds = new HashSet<Node>();
            foreach (var type in context.LocalizedContentTypes)
            {
                var keys = await Invoke<List<Guid>>(nameof(Keys), type, [context, cancellationToken]);
                foreach (var key in keys)
                {
                    items.TryAdd(new(type.Name, key), null);
                }

                foreach (var assignment in assignments.Where(item => item.EntityType == type.Name && keys.Contains(item.EntityId)))
                {
                    var node = new Node(type.Name, assignment.EntityId);
                    items[node] = assignment.Locale;
                    if (assignment.Locale is not null)
                    {
                        explicitIds.Add(node);
                    }
                }

                var entity = context.Model.FindEntityType(type)!;
                foreach (var property in entity.GetProperties().Where(property =>
                             (parentNames.Contains(property.Name) || mediaNames.Contains(property.Name))
                             && (property.ClrType == typeof(Guid) || property.ClrType == typeof(Guid?))))
                {
                    var rows = await Invoke<List<RawLink>>(nameof(Links), type,
                        [context, property.Name, property.ClrType == typeof(Guid?), mediaNames.Contains(property.Name), cancellationToken]);
                    pending.AddRange(rows.Select(row => (type.Name, property.Name, mediaNames.Contains(property.Name), row)));
                }
            }

            foreach (var link in pending)
            {
                var child = new Node(link.Media ? "VfsFile" : link.Type, link.Row.Child);
                var parents = items.Keys.Where(node => node.Id == link.Row.Parent).ToArray();
                var name = link.Property.EndsWith("Id", StringComparison.Ordinal) ? link.Property[..^2] : link.Property;
                var preferred = link.Media ? link.Type
                    : name == "Parent" && link.Type != "VfsFile" ? link.Type
                    : name == "Folder" ? "DownloadFolder"
                    : name == "Board" ? "ForumBoard"
                    : name == "Topic" ? "ForumPost"
                    : name == "ReplyTo" ? link.Type
                    : name == "Category" ? link.Type.StartsWith("Forum", StringComparison.Ordinal) ? "ForumBoardCategory"
                        : link.Type.StartsWith("Article", StringComparison.Ordinal) ? "ArticleCategory" : "NewsCategory"
                    : name;
                var exact = parents.Where(node => node.Type == preferred).ToArray();
                if (link.Type == "VfsFile" && !link.Media && link.Row.OwnerType is { } owner)
                {
                    exact = parents.Where(node => owner == node.Type || owner.EndsWith(node.Type + "s", StringComparison.Ordinal)
                        || owner == "Galleries" && node.Type == "Gallery").ToArray();
                }
                foreach (var parent in exact.Length > 0 ? exact : parents)
                {
                    links.Add(new(child, parent));
                }
            }
            foreach (var entity in context.Model.GetEntityTypes())
            {
                if (entity.FindAnnotation("Behind:LocaleOwnerType")?.Value is not string owner)
                {
                    continue;
                }
                links.AddRange(items.Keys.Where(node => node.Type == entity.ClrType.Name)
                    .Select(node => new Link(node, new(owner, node.Id))));
            }

            var inherited = explicitIds.ToHashSet();
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var link in links.Where(link => items.ContainsKey(link.Child) && items.ContainsKey(link.Parent)))
                {
                    if (!inherited.Contains(link.Child) && inherited.Contains(link.Parent))
                    {
                        items[link.Child] = items[link.Parent];
                        inherited.Add(link.Child);
                        changed = true;
                    }
                }
            }

            var hidden = items.Where(item => item.Value is not null
                    && !string.Equals(item.Value, locale, StringComparison.Ordinal))
                .Select(item => item.Key).ToHashSet();
            changed = true;
            while (changed)
            {
                changed = false;
                foreach (var link in links)
                {
                    if (hidden.Contains(link.Parent) && hidden.Add(link.Child))
                    {
                        changed = true;
                    }
                }
            }

            return new ContentLocaleScope(hidden.Select(node => node.Key).ToArray(),
                items.ToDictionary(item => item.Key.Key, item => item.Value, StringComparer.Ordinal));
        }
    }

    private static async Task<T> Invoke<T>(string name, Type type, object[] arguments)
    {
        var method = typeof(ContentLocaleExtensions).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(type);
        return await (Task<T>)method.Invoke(null, arguments)!;
    }

    private static Task<bool> Exists<TEntity>(DbContext context, Guid id, CancellationToken cancellationToken)
        where TEntity : class => context.Set<TEntity>().IgnoreQueryFilters([FilterName])
            .AnyAsync(row => EF.Property<Guid>(row, context.ContentKeyProperty(typeof(TEntity))) == id, cancellationToken);

    private static Task<List<Guid>> Keys<TEntity>(DbContext context, CancellationToken cancellationToken)
        where TEntity : class => Latest<TEntity>(context)
            .Select(row => EF.Property<Guid>(row, context.ContentKeyProperty(typeof(TEntity)))).Distinct().ToListAsync(cancellationToken);

    private static Task<List<RawLink>> Links<TEntity>(
        DbContext context, string property, bool nullable, bool media, CancellationToken cancellationToken)
        where TEntity : class
    {
        var query = Latest<TEntity>(context);
        var keyProperty = context.ContentKeyProperty(typeof(TEntity));
        var hasType = context.Model.FindEntityType(typeof(TEntity))!.FindProperty("Type")?.ClrType == typeof(string);
        query = nullable
            ? query.Where(row => EF.Property<Guid?>(row, property).HasValue)
            : query.Where(row => EF.Property<Guid>(row, property) != Guid.Empty);
        return query.Select(row => new RawLink(
                media ? nullable ? EF.Property<Guid?>(row, property)!.Value : EF.Property<Guid>(row, property)
                    : EF.Property<Guid>(row, keyProperty),
                media ? EF.Property<Guid>(row, keyProperty)
                    : nullable ? EF.Property<Guid?>(row, property)!.Value : EF.Property<Guid>(row, property),
                hasType ? EF.Property<string>(row, "Type") : null))
            .Distinct().ToListAsync(cancellationToken);
    }

    private static IQueryable<TEntity> Latest<TEntity>(DbContext context) where TEntity : class
    {
        var source = context.Set<TEntity>().IgnoreQueryFilters([FilterName]);
        var key = context.ContentKeyProperty(typeof(TEntity));
        return source.Where(row => !source.Any(other =>
            EF.Property<Guid>(other, key) == EF.Property<Guid>(row, key)
            && EF.Property<long>(other, VersionedColumns.Version) > EF.Property<long>(row, VersionedColumns.Version)));
    }

    private static Task<bool> SlugConflict<TEntity>(
        DbContext context, Guid id, string? locale, CancellationToken cancellationToken) where TEntity : class
    {
        var source = Latest<TEntity>(context);
        var rows = ((IContentLocaleStore)context).InContentLocale(source, typeof(TEntity).Name, locale);
        var key = context.ContentKeyProperty(typeof(TEntity));
        var parent = context.Model.FindEntityType(typeof(TEntity))!.FindProperty("ParentId")?.ClrType == typeof(Guid?);
        return rows.AnyAsync(candidate => EF.Property<Guid>(candidate, key) != id && source.Any(current =>
            EF.Property<Guid>(current, key) == id
            && EF.Property<string>(current, "Slug") == EF.Property<string>(candidate, "Slug")
            && (!parent || EF.Property<Guid?>(current, "ParentId") == EF.Property<Guid?>(candidate, "ParentId"))),
            cancellationToken);
    }
}
