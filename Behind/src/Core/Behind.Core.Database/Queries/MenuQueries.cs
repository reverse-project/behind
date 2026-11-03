using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class MenuQueries(AbstractCoreContext context) : IMenuQueries
{
    public async Task<PagedResult<IMenuQueries.MenuListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Matching(search, state).InBucket(menu => menu.Title, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(Ordered(matching))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var counts = await ItemCountsAsync(rows.Select(row => row.Key).ToList(), cancellationToken);

        return new PagedResult<IMenuQueries.MenuListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            rows.Count > 0
                ? Some<IEnumerable<IMenuQueries.MenuListing>>(rows.Select(row => ToListing(row, counts)).ToList())
                : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Matching(search, state).LettersAsync(menu => menu.Title, cancellationToken);
    }

    public async Task<Option<IMenuQueries.MenuListing>> Find(
        Guid menuId,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        var rows = await Ordered(Matching(None, state).OfKey(menuId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var counts = await ItemCountsAsync([menuId], cancellationToken);

        return rows.Head().Map(row => ToListing(row, counts));
    }

    public async Task<IReadOnlyList<IMenuQueries.MenuView>> Resolve(
        IReadOnlyCollection<string> names,
        Option<Guid> viewerId,
        CancellationToken cancellationToken = default)
    {
        var wanted = names.Select(name => name.Trim().ToLowerInvariant()).Distinct().ToList();

        var menus = await Ordered(context.ActiveMenus().LatestOf(context.Menus).Where(menu => wanted.Contains(menu.Name)))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (menus.Count == 0)
        {
            return [];
        }

        var menuIds = menus.Select(menu => menu.Key).ToList();

        var items = await context.ToListingsAsync(
            context.LatestMenuItemsIn(DeletionState.Active)
                .Where(item => menuIds.Contains(EF.Property<Guid>(item, MenuItemConfiguration.MenuIdProperty))),
            cancellationToken);

        var viewerRoleIds = await ViewerRoleIdsAsync(viewerId, cancellationToken);

        return menus
            .Select(menu => new IMenuQueries.MenuView(
                menu.Key,
                menu.Name,
                menu.Title,
                items.Where(item => item.MenuId == menu.Key).ToVisibleTree(viewerRoleIds)))
            .ToList();
    }

    private async Task<IReadOnlySet<Guid>> ViewerRoleIdsAsync(Option<Guid> viewerId, CancellationToken cancellationToken) =>
        await viewerId.Match(
            () => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>()),
            async userId => (IReadOnlySet<Guid>)(await context.FindRolesOfUserAsync(userId, cancellationToken))
                .Select(role => role.Id)
                .ToHashSet());

    private async Task<IReadOnlyDictionary<Guid, long>> ItemCountsAsync(
        IReadOnlyCollection<Guid> menuIds,
        CancellationToken cancellationToken)
    {
        var counts = await context.LatestMenuItemsIn(DeletionState.Active)
            .Where(item => menuIds.Contains(EF.Property<Guid>(item, MenuItemConfiguration.MenuIdProperty)))
            .GroupBy(item => EF.Property<Guid>(item, MenuItemConfiguration.MenuIdProperty))
            .Select(group => new { MenuId = group.Key, Count = group.LongCount() })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(count => count.MenuId, count => count.Count);
    }

    private static IQueryable<MenuRow> Ordered(IQueryable<Menu> menus) =>
        menus
            .OrderBy(menu => menu.Title.ToLower())
            .ThenBy(menu => EF.Property<Guid>(menu, VersionedColumns.Key))
            .Select(menu => new MenuRow(
                EF.Property<Guid>(menu, VersionedColumns.Key),
                EF.Property<long>(menu, VersionedColumns.Version),
                menu.Name,
                menu.Title));

    private static IMenuQueries.MenuListing ToListing(MenuRow row, IReadOnlyDictionary<Guid, long> counts) =>
        new(new VersionedKey<Guid>(row.Key, row.Version), row.Name, row.Title, counts.GetValueOrDefault(row.Key));

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<Menu> Matching(Option<string> search, DeletionState state)
    {
        var latest = context.Menus
            .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
            .LatestOf(context.Menus);

        return search
            .Map(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .Match(
                () => latest,
                term => latest.Where(menu => menu.Title.ToLower().Contains(term) || menu.Name.Contains(term)));
    }

    private sealed record MenuRow(Guid Key, long Version, string Name, string Title);
}
