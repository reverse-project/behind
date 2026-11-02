using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database;

internal sealed record ArticleRow(
    Guid Key,
    long Version,
    string Title,
    string Summary,
    string? Content,
    Guid AuthorId,
    Guid? CategoryId)
{
    public VersionedKey<Guid> Id => new(Key, Version);
}

internal sealed record ArticleLookups(
    IReadOnlyDictionary<Guid, string> Authors,
    IReadOnlyDictionary<Guid, IArticleQueries.ArticleCategoryInfo> Categories,
    IReadOnlyDictionary<Guid, AuditTimes> Times)
{
    public string AuthorOf(ArticleRow row) => Authors.GetValueOrDefault(row.AuthorId, string.Empty);

    public Option<IArticleQueries.ArticleCategoryInfo> CategoryOf(ArticleRow row) =>
        row.CategoryId.ToOption().Bind(id => Categories.TryGetValue(id, out var category)
            ? Some(category)
            : FunctionalExtensions.None);

    public Option<DateTimeOffset> CreatedAtOf(ArticleRow row) =>
        Times.TryGetValue(row.Key, out var times) ? times.CreatedAt : FunctionalExtensions.None;

    public Option<DateTimeOffset> UpdatedAtOf(ArticleRow row) =>
        Times.TryGetValue(row.Key, out var times) ? times.UpdatedAt : FunctionalExtensions.None;
}

internal static class ArticleQueryExtensions
{
    extension(IQueryable<Article> articles)
    {
        public IQueryable<ArticleRow> ToRows() =>
            articles.Select(item => new ArticleRow(
                EF.Property<Guid>(item, VersionedColumns.Key),
                EF.Property<long>(item, VersionedColumns.Version),
                item.Title,
                item.Summary,
                null,
                EF.Property<Guid>(item, ArticleConfiguration.AuthorIdProperty),
                EF.Property<Guid?>(item, ArticleConfiguration.CategoryIdProperty)));

        public IQueryable<ArticleRow> ToDetailRows() =>
            articles.Select(item => new ArticleRow(
                EF.Property<Guid>(item, VersionedColumns.Key),
                EF.Property<long>(item, VersionedColumns.Version),
                item.Title,
                item.Summary,
                item.Content,
                EF.Property<Guid>(item, ArticleConfiguration.AuthorIdProperty),
                EF.Property<Guid?>(item, ArticleConfiguration.CategoryIdProperty)));

        public IQueryable<Article> Matching(Option<string> search) =>
            search
                .Map(term => term.Trim().ToLowerInvariant())
                .Where(term => term.Length > 0)
                .Match(
                    () => articles,
                    term => articles.Where(item =>
                        item.Title.ToLower().Contains(term) || item.Content.ToLower().Contains(term)));
    }

    extension(AbstractCoreContext context)
    {
        public IQueryable<Article> ActiveArticles() =>
            context.Articles.InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active);

        public IQueryable<Article> LiveArticles() =>
            context.ActiveArticles().LiveIn(context.ActiveVersions, Article.EntityType);

        public IQueryable<ArticleCategory> ActiveArticleCategories() =>
            context.ArticleCategories.InDeletionState(
                context,
                AbstractCoreContext.DeletionRecorded,
                DeletionState.Active);

        public async Task<ArticleLookups> LookupsAsync(
            IReadOnlyCollection<ArticleRow> rows,
            CancellationToken cancellationToken)
        {
            var categoryKeys = rows.Select(row => row.CategoryId).OfType<Guid>().Distinct().ToList();

            var categories = await context.ActiveArticleCategories()
                .OfKeys(categoryKeys)
                .LatestOf(context.ArticleCategories)
                .Select(category => new
                {
                    Key = EF.Property<Guid>(category, VersionedColumns.Key),
                    category.Name,
                    ImageId = EF.Property<Guid?>(category, ArticleCategoryConfiguration.HeaderImageIdProperty)
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return new ArticleLookups(
                await context.DisplayNamesAsync(rows.Select(row => row.AuthorId).Distinct().ToList(), cancellationToken),
                categories.ToDictionary(
                    item => item.Key,
                    item => new IArticleQueries.ArticleCategoryInfo(item.Key, item.Name, item.ImageId.ToOption())),
                await context.AuditTimesAsync<Article>(
                    rows.ToDictionary(row => row.Key, row => row.Version),
                    cancellationToken));
        }
    }
}
