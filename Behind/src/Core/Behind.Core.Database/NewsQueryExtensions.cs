using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database;

internal sealed record NewsRow(
    Guid Key,
    long Version,
    string Title,
    string Summary,
    string? Content,
    Guid AuthorId,
    Guid? CategoryId,
    bool Highlighted,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt)
{
    public VersionedKey<Guid> Id => new(Key, Version);

    public Option<DateTimeOffset> StartAtOption => StartAt.ToOption();

    public Option<DateTimeOffset> EndAtOption => EndAt.ToOption();
}

internal sealed record NewsLookups(
    IReadOnlyDictionary<Guid, string> Authors,
    IReadOnlyDictionary<Guid, INewsQueries.NewsCategoryInfo> Categories,
    IReadOnlyDictionary<Guid, long> CommentCounts,
    IReadOnlyDictionary<Guid, AuditTimes> Times)
{
    public string AuthorOf(NewsRow row) => Authors.GetValueOrDefault(row.AuthorId, string.Empty);

    public Option<INewsQueries.NewsCategoryInfo> CategoryOf(NewsRow row) =>
        row.CategoryId.ToOption().Bind(id => Categories.TryGetValue(id, out var category)
            ? Some(category)
            : FunctionalExtensions.None);

    public long CommentsOf(NewsRow row) => CommentCounts.GetValueOrDefault(row.Key);

    public Option<DateTimeOffset> CreatedAtOf(NewsRow row) =>
        Times.TryGetValue(row.Key, out var times) ? times.CreatedAt : FunctionalExtensions.None;

    public Option<DateTimeOffset> UpdatedAtOf(NewsRow row) =>
        Times.TryGetValue(row.Key, out var times) ? times.UpdatedAt : FunctionalExtensions.None;
}

internal static class NewsQueryExtensions
{
    extension(IQueryable<News> news)
    {
        public IQueryable<NewsRow> ToRows() =>
            news.Select(item => new NewsRow(
                EF.Property<Guid>(item, VersionedColumns.Key),
                EF.Property<long>(item, VersionedColumns.Version),
                item.Title,
                item.Summary,
                null,
                EF.Property<Guid>(item, NewsConfiguration.AuthorIdProperty),
                EF.Property<Guid?>(item, NewsConfiguration.CategoryIdProperty),
                item.Highlighted,
                EF.Property<DateTimeOffset?>(item, NewsConfiguration.StartAtProperty),
                EF.Property<DateTimeOffset?>(item, NewsConfiguration.EndAtProperty)));

        public IQueryable<NewsRow> ToDetailRows() =>
            news.Select(item => new NewsRow(
                EF.Property<Guid>(item, VersionedColumns.Key),
                EF.Property<long>(item, VersionedColumns.Version),
                item.Title,
                item.Summary,
                item.Content,
                EF.Property<Guid>(item, NewsConfiguration.AuthorIdProperty),
                EF.Property<Guid?>(item, NewsConfiguration.CategoryIdProperty),
                item.Highlighted,
                EF.Property<DateTimeOffset?>(item, NewsConfiguration.StartAtProperty),
                EF.Property<DateTimeOffset?>(item, NewsConfiguration.EndAtProperty)));

        public IQueryable<News> ActiveAt(DateTimeOffset now) =>
            news.Where(item => (EF.Property<DateTimeOffset?>(item, NewsConfiguration.StartAtProperty) == null
                                || EF.Property<DateTimeOffset?>(item, NewsConfiguration.StartAtProperty) <= now)
                               && (EF.Property<DateTimeOffset?>(item, NewsConfiguration.EndAtProperty) == null
                                   || EF.Property<DateTimeOffset?>(item, NewsConfiguration.EndAtProperty) > now));
    }

    extension(AbstractCoreContext context)
    {
        public IQueryable<News> ActiveNews() =>
            context.NewsEntries.InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active);

        public IQueryable<News> LiveNews() =>
            context.ActiveNews().LiveIn(context.ActiveVersions, News.EntityType);

        public IQueryable<NewsComment> ActiveComments() =>
            context.NewsComments.InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active);

        public IQueryable<NewsCategory> ActiveCategories() =>
            context.NewsCategories.InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active);

        public async Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken)
        {
            var names = await context.Users
                .Where(user => userIds.Contains(EF.Property<Guid>(user, "Id")))
                .Select(user => new { Id = EF.Property<Guid>(user, "Id"), user.DisplayName })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return names.ToDictionary(item => item.Id, item => item.DisplayName);
        }

        public async Task<NewsLookups> LookupsAsync(
            IReadOnlyCollection<NewsRow> rows,
            CancellationToken cancellationToken)
        {
            var newsKeys = rows.Select(row => row.Key).ToList();
            var categoryKeys = rows.Select(row => row.CategoryId).OfType<Guid>().Distinct().ToList();

            var categories = await context.ActiveCategories()
                .OfKeys(categoryKeys)
                .LatestOf(context.NewsCategories)
                .Select(category => new
                {
                    Key = EF.Property<Guid>(category, VersionedColumns.Key),
                    category.Name,
                    ImageId = EF.Property<Guid?>(category, NewsCategoryConfiguration.HeaderImageIdProperty)
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var counts = await context.ActiveComments()
                .Where(comment => newsKeys.Contains(EF.Property<Guid>(comment, NewsCommentConfiguration.NewsIdProperty)))
                .LatestOf(context.NewsComments)
                .GroupBy(comment => EF.Property<Guid>(comment, NewsCommentConfiguration.NewsIdProperty))
                .Select(group => new { NewsId = group.Key, Count = group.LongCount() })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return new NewsLookups(
                await context.DisplayNamesAsync(rows.Select(row => row.AuthorId).Distinct().ToList(), cancellationToken),
                categories.ToDictionary(
                    item => item.Key,
                    item => new INewsQueries.NewsCategoryInfo(item.Key, item.Name, item.ImageId.ToOption())),
                counts.ToDictionary(item => item.NewsId, item => item.Count),
                await context.AuditTimesAsync<News>(
                    rows.ToDictionary(row => row.Key, row => row.Version),
                    cancellationToken));
        }
    }
}
