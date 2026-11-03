using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class NewsQueries(AbstractCoreContext context) : INewsQueries
{
    public async Task<PagedResult<INewsQueries.NewsListing>> List(
        Flipper flipper,
        Option<Guid> category,
        Option<string> letter,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var matching = Live(category, now).InBucket(news => news.Title, letter);

        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(
                context.ThenByCreation(
                        matching.OrderByDescending(news => news.Highlighted),
                        newestFirst: true,
                        overriddenBy: StartOf)
                    .ToRows())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var lookups = await context.LookupsAsync(rows, cancellationToken);

        return new PagedResult<INewsQueries.NewsListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            rows.Count > 0
                ? Some<IEnumerable<INewsQueries.NewsListing>>(rows.Select(row => ToListing(row, lookups)).ToList())
                : None);
    }

    public async Task<IReadOnlyList<string>> GetLiveLetters(
        Option<Guid> category,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        await Live(category, now).LettersAsync(news => news.Title, cancellationToken);

    public async Task<PagedResult<INewsQueries.ManagedNewsListing>> ListManaged(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Latest(search, state).InBucket(news => news.Title, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(
                context.OrderByCreation(matching, newestFirst: true, overriddenBy: StartOf).ToRows())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var keys = rows.Select(row => row.Key).ToList();
        var lookups = await context.LookupsAsync(rows, cancellationToken);
        var active = await context.ActiveVersionsAsync(News.EntityType, keys.Select(key => key.ToString()).ToList(), cancellationToken);
        var live = await LiveRowsAsync(keys, cancellationToken);

        var listings = rows
            .Select(row => new INewsQueries.ManagedNewsListing(
                row.Key,
                row.Version,
                active.TryGetValue(row.Key.ToString(), out var version) ? Some(version) : None,
                row.Title,
                lookups.AuthorOf(row),
                lookups.CreatedAtOf(row),
                lookups.UpdatedAtOf(row),
                lookups.CategoryOf(row),
                lookups.CommentsOf(row),
                live.TryGetValue(row.Key, out var liveRow)
                    ? Some(new INewsQueries.NewsLiveInfo(liveRow.StartAtOption, liveRow.EndAtOption, liveRow.Highlighted))
                    : None))
            .ToList();

        return new PagedResult<INewsQueries.ManagedNewsListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            listings.Count > 0 ? Some<IEnumerable<INewsQueries.ManagedNewsListing>>(listings) : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Latest(search, state).LettersAsync(news => news.Title, cancellationToken);
    }

    public async Task<Option<INewsQueries.NewsDetail>> Find(
        Guid id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.LiveNews()
            .ActiveAt(now)
            .OfKey(id)
            .ToDetailRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return await ToDetail(rows, cancellationToken);
    }

    public async Task<Option<INewsQueries.NewsHistory>> FindHistory(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.ActiveNews()
            .OfKey(id)
            .ToRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return None;
        }

        var active = (await context.ActiveVersionsAsync(News.EntityType, [id.ToString()], cancellationToken))
            .Select(pair => pair.Value)
            .Head();

        var created = await context.VersionCreationTimesAsync<News>(
            id,
            rows.Select(row => row.Version).ToList(),
            cancellationToken);

        return Some(new INewsQueries.NewsHistory(
            id,
            active,
            rows.OrderByDescending(row => row.Version)
                .Select(row => new INewsQueries.NewsVersionInfo(
                    row.Version,
                    row.Title,
                    row.StartAtOption,
                    row.EndAtOption,
                    row.Highlighted,
                    created.TryGetValue(row.Version, out var at) ? Some(at) : None,
                    active.Match(() => false, version => version == row.Version)))
                .ToList()));
    }

    public async Task<Option<INewsQueries.NewsDetail>> FindVersion(
        VersionedKey<Guid> id,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.ActiveNews()
            .OfVersion(id)
            .ToDetailRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return await ToDetail(rows, cancellationToken);
    }

    private async Task<Option<INewsQueries.NewsDetail>> ToDetail(
        IReadOnlyList<NewsRow> rows,
        CancellationToken cancellationToken)
    {
        var lookups = await context.LookupsAsync(rows, cancellationToken);

        return rows.Head().Map(row => new INewsQueries.NewsDetail(
            row.Id,
            row.Title,
            row.Summary,
            row.Content ?? string.Empty,
            lookups.AuthorOf(row),
            lookups.CreatedAtOf(row),
            lookups.UpdatedAtOf(row),
            lookups.CategoryOf(row),
            row.Highlighted,
            row.StartAtOption,
            row.EndAtOption));
    }

    private async Task<IReadOnlyDictionary<Guid, NewsRow>> LiveRowsAsync(
        IReadOnlyCollection<Guid> keys,
        CancellationToken cancellationToken)
    {
        var rows = await context.LiveNews().OfKeys(keys).ToRows().AsNoTracking().ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Key);
    }

    private static INewsQueries.NewsListing ToListing(NewsRow row, NewsLookups lookups) =>
        new(
            row.Id,
            row.Title,
            row.Summary,
            lookups.AuthorOf(row),
            lookups.CreatedAtOf(row),
            lookups.UpdatedAtOf(row),
            lookups.CategoryOf(row),
            lookups.CommentsOf(row),
            row.Highlighted,
            row.StartAtOption,
            row.EndAtOption);

    private static readonly System.Linq.Expressions.Expression<Func<News, DateTimeOffset?>> StartOf =
        news => EF.Property<DateTimeOffset?>(news, NewsConfiguration.StartAtProperty);

    private IQueryable<News> Live(Option<Guid> category, DateTimeOffset now)
    {
        var live = context.LiveNews().ActiveAt(now);

        return category.Match(
            () => live,
            categoryId => live.Where(news =>
                EF.Property<Guid?>(news, NewsConfiguration.CategoryIdProperty) == categoryId));
    }

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<News> Latest(Option<string> search, DeletionState state)
    {
        var latest = context.NewsEntries
            .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
            .LatestOf(context.NewsEntries);

        return search
            .Map(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .Match(
                () => latest,
                term => latest.Where(news => news.Title.ToLower().Contains(term)));
    }
}
