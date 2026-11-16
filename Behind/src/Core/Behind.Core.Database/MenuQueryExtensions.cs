using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

internal sealed record MenuItemRow(
    Guid Key,
    long Version,
    Guid MenuId,
    Guid? ParentId,
    string Title,
    string Url,
    MenuTarget Target,
    MenuItemKind Kind,
    int Position);

internal static class MenuQueryExtensions
{
    extension(IQueryable<MenuItem> items)
    {
        public IOrderedQueryable<MenuItem> InListingOrder() =>
            items.OrderBy(item => item.Position)
                .ThenBy(item => item.Title.ToLower())
                .ThenBy(item => EF.Property<Guid>(item, VersionedColumns.Key));

        public IQueryable<MenuItemRow> ToRows() =>
            items.Select(item => new MenuItemRow(
                EF.Property<Guid>(item, VersionedColumns.Key),
                EF.Property<long>(item, VersionedColumns.Version),
                EF.Property<Guid>(item, MenuItemConfiguration.MenuIdProperty),
                EF.Property<Guid?>(item, MenuItemConfiguration.ParentIdProperty),
                item.Title,
                item.Url,
                item.Target,
                item.Kind,
                item.Position));
    }

    extension(IEnumerable<MenuItemRow> rows)
    {
        public IReadOnlyList<IMenuItemQueries.MenuItemListing> ToListings(
            ILookup<(Guid Key, long Version), Guid> roles) =>
            rows
                .OrderBy(row => row.Position)
                .ThenBy(row => row.Title.ToLowerInvariant())
                .ThenBy(row => row.Key)
                .Select(row => new IMenuItemQueries.MenuItemListing(
                    new VersionedKey<Guid>(row.Key, row.Version),
                    row.MenuId,
                    row.ParentId.ToOption(),
                    row.Title,
                    row.Url,
                    row.Target,
                    row.Kind,
                    row.Position,
                    roles[(row.Key, row.Version)].Order().ToList()))
                .ToList();
    }

    extension(AbstractCoreContext context)
    {
        public IQueryable<Menu> ActiveMenus() =>
            context.Menus.InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active);

        public IQueryable<MenuItem> ActiveMenuItems() =>
            context.MenuItems.InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active);

        public IQueryable<MenuItem> LatestMenuItemsIn(DeletionState state) =>
            context.MenuItems
                .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
                .LatestOf(context.MenuItems);

        public async Task<ILookup<(Guid Key, long Version), Guid>> RolesOfMenuItemsAsync(
            IReadOnlyCollection<Guid> itemKeys,
            CancellationToken cancellationToken)
        {
            var found = await context.MenuItemRoles
                .Where(role => itemKeys.Contains(EF.Property<Guid>(role, MenuItemConfiguration.RolesItemKeyProperty)))
                .Select(role => new
                {
                    Key = EF.Property<Guid>(role, MenuItemConfiguration.RolesItemKeyProperty),
                    Version = EF.Property<long>(role, MenuItemConfiguration.RolesItemVersionProperty),
                    RoleId = EF.Property<Guid>(role, MenuItemConfiguration.RolesRoleIdProperty)
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return found.ToLookup(role => (role.Key, role.Version), role => role.RoleId);
        }

        public async Task<IReadOnlyList<IMenuItemQueries.MenuItemListing>> ToListingsAsync(
            IQueryable<MenuItem> items,
            CancellationToken cancellationToken)
        {
            var rows = await items.ToRows().AsNoTracking().ToListAsync(cancellationToken);

            return rows.ToListings(
                await context.RolesOfMenuItemsAsync(rows.Select(row => row.Key).Distinct().ToList(), cancellationToken));
        }
    }
}
