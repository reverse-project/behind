using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class NewsCategoryQueries(AbstractCoreContext context) : INewsCategoryQueries
{
    public async Task<PagedResult<INewsCategoryQueries.NewsCategoryListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Matching(search, state).InBucket(category => category.Name, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var listings = await flipper.Apply(Ordered(matching))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<INewsCategoryQueries.NewsCategoryListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            listings.Count > 0
                ? Some<IEnumerable<INewsCategoryQueries.NewsCategoryListing>>(listings.Select(ToListing).ToList())
                : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Matching(search, state).LettersAsync(category => category.Name, cancellationToken);
    }

    public async Task<IReadOnlyList<INewsCategoryQueries.NewsCategoryListing>> ListActive(
        CancellationToken cancellationToken = default)
    {
        var rows = await Ordered(context.ActiveCategories().LatestOf(context.NewsCategories))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Select(ToListing).ToList();
    }

    private static IQueryable<CategoryRow> Ordered(IQueryable<NewsCategory> categories) =>
        categories
            .OrderBy(category => category.Name.ToLower())
            .ThenBy(category => EF.Property<Guid>(category, VersionedColumns.Key))
            .Select(category => new CategoryRow(
                EF.Property<Guid>(category, VersionedColumns.Key),
                EF.Property<long>(category, VersionedColumns.Version),
                category.Name,
                EF.Property<Guid?>(category, NewsCategoryConfiguration.HeaderImageIdProperty)));

    private static INewsCategoryQueries.NewsCategoryListing ToListing(CategoryRow row) =>
        new(new VersionedKey<Guid>(row.Key, row.Version), row.Name, row.HeaderImageId.ToOption());

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<NewsCategory> Matching(Option<string> search, DeletionState state)
    {
        var latest = context.NewsCategories
            .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
            .LatestOf(context.NewsCategories);

        return search
            .Map(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .Match(
                () => latest,
                term => latest.Where(category => category.Name.ToLower().Contains(term)));
    }

    private sealed record CategoryRow(Guid Key, long Version, string Name, Guid? HeaderImageId);
}
