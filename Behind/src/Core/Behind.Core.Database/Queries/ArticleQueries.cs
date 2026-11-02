using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class ArticleQueries(AbstractCoreContext context) : IArticleQueries
{
    public async Task<PagedResult<IArticleQueries.ArticleListing>> List(
        Flipper flipper,
        Option<Guid> category,
        Option<string> search,
        CancellationToken cancellationToken = default)
    {
        var live = context.LiveArticles().Matching(search);

        var matching = category.Match(
            () => live,
            categoryId => live.Where(article =>
                EF.Property<Guid?>(article, ArticleConfiguration.CategoryIdProperty) == categoryId));

        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(context.OrderByCreation(matching, newestFirst: true).ToRows())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var lookups = await context.LookupsAsync(rows, cancellationToken);

        return new PagedResult<IArticleQueries.ArticleListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            rows.Count > 0
                ? Some<IEnumerable<IArticleQueries.ArticleListing>>(rows.Select(row => ToListing(row, lookups)).ToList())
                : None);
    }

    public async Task<PagedResult<IArticleQueries.ManagedArticleListing>> ListManaged(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Latest(search, state).InBucket(article => article.Title, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(context.OrderByCreation(matching, newestFirst: true).ToRows())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var keys = rows.Select(row => row.Key.ToString()).ToList();
        var lookups = await context.LookupsAsync(rows, cancellationToken);
        var active = await context.ActiveVersionsAsync(Article.EntityType, keys, cancellationToken);

        var listings = rows
            .Select(row => new IArticleQueries.ManagedArticleListing(
                row.Key,
                row.Version,
                active.TryGetValue(row.Key.ToString(), out var version) ? Some(version) : None,
                row.Title,
                lookups.AuthorOf(row),
                lookups.CreatedAtOf(row),
                lookups.UpdatedAtOf(row),
                lookups.CategoryOf(row)))
            .ToList();

        return new PagedResult<IArticleQueries.ManagedArticleListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            listings.Count > 0 ? Some<IEnumerable<IArticleQueries.ManagedArticleListing>>(listings) : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Latest(search, state).LettersAsync(article => article.Title, cancellationToken);
    }

    public async Task<Option<IArticleQueries.ArticleDetail>> Find(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.LiveArticles()
            .OfKey(id)
            .ToDetailRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return await ToDetail(rows, cancellationToken);
    }

    public async Task<Option<IArticleQueries.ArticleHistory>> FindHistory(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.ActiveArticles()
            .OfKey(id)
            .ToRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return None;
        }

        var active = (await context.ActiveVersionsAsync(Article.EntityType, [id.ToString()], cancellationToken))
            .Select(pair => pair.Value)
            .Head();

        var created = await context.VersionCreationTimesAsync<Article>(
            id,
            rows.Select(row => row.Version).ToList(),
            cancellationToken);

        return Some(new IArticleQueries.ArticleHistory(
            id,
            active,
            rows.OrderByDescending(row => row.Version)
                .Select(row => new IArticleQueries.ArticleVersionInfo(
                    row.Version,
                    row.Title,
                    created.TryGetValue(row.Version, out var at) ? Some(at) : None,
                    active.Match(() => false, version => version == row.Version)))
                .ToList()));
    }

    public async Task<Option<IArticleQueries.ArticleDetail>> FindVersion(
        VersionedKey<Guid> id,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.ActiveArticles()
            .OfVersion(id)
            .ToDetailRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return await ToDetail(rows, cancellationToken);
    }

    private async Task<Option<IArticleQueries.ArticleDetail>> ToDetail(
        IReadOnlyList<ArticleRow> rows,
        CancellationToken cancellationToken)
    {
        var lookups = await context.LookupsAsync(rows, cancellationToken);

        return rows.Head().Map(row => new IArticleQueries.ArticleDetail(
            row.Id,
            row.Title,
            row.Summary,
            row.Content ?? string.Empty,
            lookups.AuthorOf(row),
            lookups.CreatedAtOf(row),
            lookups.UpdatedAtOf(row),
            lookups.CategoryOf(row)));
    }

    private static IArticleQueries.ArticleListing ToListing(ArticleRow row, ArticleLookups lookups) =>
        new(
            row.Id,
            row.Title,
            row.Summary,
            lookups.AuthorOf(row),
            lookups.CreatedAtOf(row),
            lookups.UpdatedAtOf(row),
            lookups.CategoryOf(row));

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<Article> Latest(Option<string> search, DeletionState state) =>
        context.Articles
            .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
            .LatestOf(context.Articles)
            .Matching(search);
}
