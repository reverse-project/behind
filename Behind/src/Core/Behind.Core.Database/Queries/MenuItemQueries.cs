using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class MenuItemQueries(AbstractCoreContext context) : IMenuItemQueries
{
    public async Task<PagedResult<IMenuItemQueries.MenuItemListing>> ListOfMenu(
        Guid menuId,
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Matching(menuId, search, state).InBucket(item => item.Title, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var listings = await context.ToListingsAsync(flipper.Apply(matching.InListingOrder()), cancellationToken);

        return new PagedResult<IMenuItemQueries.MenuItemListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            listings.Count > 0 ? Some<IEnumerable<IMenuItemQueries.MenuItemListing>>(listings) : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Guid menuId,
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Matching(menuId, search, state).LettersAsync(item => item.Title, cancellationToken);
    }

    public async Task<IReadOnlyList<IMenuItemQueries.MenuItemListing>> ListParents(
        Guid menuId,
        CancellationToken cancellationToken = default) =>
        await context.ToListingsAsync(
            context.LatestMenuItemsIn(DeletionState.Active)
                .Where(item => EF.Property<Guid>(item, MenuItemConfiguration.MenuIdProperty) == menuId
                               && EF.Property<Guid?>(item, MenuItemConfiguration.ParentIdProperty) == null
                               && item.Kind == MenuItemKind.Link),
            cancellationToken);

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<MenuItem> Matching(Guid menuId, Option<string> search, DeletionState state)
    {
        var items = context.LatestMenuItemsIn(state)
            .Where(item => EF.Property<Guid>(item, MenuItemConfiguration.MenuIdProperty) == menuId);

        return search
            .Map(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .Match(
                () => items,
                term => items.Where(item => item.Title.ToLower().Contains(term) || item.Url.ToLower().Contains(term)));
    }
}
