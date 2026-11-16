using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Behind.Common.Database;

public sealed record ContentItemLabel(Guid Id, string DisplayName);

public sealed record ContentItemPage(long Total, int Page, long PageCount, int PageSize, IReadOnlyList<ContentItemLabel> Items);

public static class ContentItemBrowserExtensions
{
    private static readonly string[] LabelProperties =
        ["Title", "DisplayName", "FileName", "Name", "Description", "Text", "Content", "Body", "Slug"];

    extension(DbContext context)
    {
        public Task<ContentItemPage> BrowseContentItems(
            Type entityType, Flipper flipper, string? search, CancellationToken cancellationToken = default)
        {
            var method = typeof(ContentItemBrowserExtensions)
                .GetMethod(nameof(Browse), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(entityType);
            return (Task<ContentItemPage>)method.Invoke(null, [context, flipper, search, cancellationToken])!;
        }
    }

    private static async Task<ContentItemPage> Browse<TEntity>(
        DbContext context, Flipper flipper, string? search, CancellationToken cancellationToken)
        where TEntity : class
    {
        var metadata = context.Model.FindEntityType(typeof(TEntity))!;
        var row = Expression.Parameter(typeof(TEntity), "item");
        Expression label = Expression.Constant("");
        foreach (var name in LabelProperties.Reverse())
        {
            if (metadata.FindProperty(name)?.ClrType != typeof(string))
            {
                continue;
            }

            var value = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(string)], row, Expression.Constant(name));
            label = Expression.Condition(
                Expression.Call(typeof(string), nameof(string.IsNullOrWhiteSpace), [], value),
                label,
                Expression.Call(value, nameof(string.Trim), []));
        }

        var key = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(Guid)], row, Expression.Constant(VersionedColumns.Key));
        var selector = Expression.Lambda<Func<TEntity, ContentItemLabel>>(
            Expression.New(typeof(ContentItemLabel).GetConstructors().Single(), [key, label],
                [typeof(ContentItemLabel).GetProperty(nameof(ContentItemLabel.Id))!,
                    typeof(ContentItemLabel).GetProperty(nameof(ContentItemLabel.DisplayName))!]), row);
        var source = context.Set<TEntity>().AsNoTracking();
        var query = source.LatestOf(source).Select(selector);
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            query = Guid.TryParse(term, out var id)
                ? query.Where(item => item.Id == id || item.DisplayName.Contains(term))
                : query.Where(item => item.DisplayName.Contains(term));
        }

        var total = await query.LongCountAsync(cancellationToken);
        var items = await flipper.Apply(query.OrderBy(item => item.DisplayName).ThenBy(item => item.Id))
            .ToListAsync(cancellationToken);
        return new ContentItemPage(total, flipper.Index, flipper.PageCount(total), flipper.Size,
            items.Select(item => item with
            {
                DisplayName = string.IsNullOrWhiteSpace(item.DisplayName)
                    ? item.Id.ToString()
                    : item.DisplayName.Length > 200 ? item.DisplayName[..200] : item.DisplayName
            }).ToArray());
    }
}
