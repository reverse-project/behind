using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class MenuItemCommands(AbstractCoreContext context) : IMenuItemCommands
{
    public Task<MenuItemSaveResult> Create(
        Guid menuId,
        IMenuItemCommands.MenuItemDraft draft,
        CancellationToken cancellationToken = default) =>
        AddVersion(Guid.CreateVersion7(), 0, menuId, draft, cancellationToken);

    public async Task<MenuItemSaveResult> Update(
        Guid itemId,
        IMenuItemCommands.MenuItemDraft draft,
        CancellationToken cancellationToken = default)
    {
        var latest = (await context.ActiveMenuItems()
                .OfKey(itemId)
                .OrderByDescending(item => EF.Property<long>(item, VersionedColumns.Version))
                .ToRows()
                .Take(1)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Head();

        return await latest.Match(
            () => Task.FromResult<MenuItemSaveResult>(new MenuItemSaveResult.NotFound()),
            row => AddVersion(itemId, row.Version, row.MenuId, draft, cancellationToken));
    }

    public async Task<bool> Delete(Guid itemId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkSoftDeletedAsync(context.MenuItems.OfKey(itemId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<bool> Restore(Guid itemId, CancellationToken cancellationToken = default)
    {
        var restored = await context.MarkRestoredAsync(context.MenuItems.OfKey(itemId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return restored > 0;
    }

    public async Task<HardDeleteResult> HardDelete(Guid itemId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkHardDeletedAsync(context.MenuItems.OfKey(itemId), cancellationToken);

        if (deleted == 0)
        {
            return HardDeleteResult.NotFound;
        }

        using (context.SoftDelete.IncludeDeleted())
        {
            var children = await context.MenuItems
                .OfKeys(await ChildKeys(itemId, cancellationToken))
                .ToListAsync(cancellationToken);

            children.ForEach(context.HardDelete);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            return HardDeleteResult.Deleted;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return HardDeleteResult.Rejected;
        }
    }

    public async Task<bool> Ensure(
        Guid itemId,
        Guid menuId,
        IMenuItemCommands.MenuItemDraft draft,
        CancellationToken cancellationToken = default) =>
        !await ExistsInAnyState(itemId, cancellationToken)
        && await AddVersion(itemId, 0, menuId, draft, cancellationToken) is MenuItemSaveResult.Saved;

    private async Task<MenuItemSaveResult> AddVersion(
        Guid key,
        long previousVersion,
        Guid menuId,
        IMenuItemCommands.MenuItemDraft draft,
        CancellationToken cancellationToken)
    {
        var rejection = await Rejection(key, previousVersion, menuId, draft, cancellationToken);

        if (rejection is not null)
        {
            return rejection;
        }

        var id = new VersionedKey<Guid>(key, previousVersion + 1);
        var item = new MenuItem(draft.Title, draft.Url, draft.Target, draft.Kind, draft.Position);

        await context.MenuItems.AddAsync(item, cancellationToken);

        var entry = context.Entry(item);
        entry.StampVersionedKey(id.Key, id.Version);
        entry.Property<Guid>(MenuItemConfiguration.MenuIdProperty).CurrentValue = menuId;
        entry.Property<Guid?>(MenuItemConfiguration.ParentIdProperty).CurrentValue = draft.ParentId.ToNullable();

        await context.MenuItemRoles.AddRangeAsync(
            draft.RoleIds.Distinct().Select(roleId => RoleRow(id, roleId)),
            cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new MenuItemSaveResult.VersionConflict();
        }

        return new MenuItemSaveResult.Saved(id);
    }

    private static Dictionary<string, object> RoleRow(VersionedKey<Guid> id, Guid roleId) =>
        new()
        {
            [MenuItemConfiguration.RolesItemKeyProperty] = id.Key,
            [MenuItemConfiguration.RolesItemVersionProperty] = id.Version,
            [MenuItemConfiguration.RolesRoleIdProperty] = roleId
        };

    private async Task<MenuItemSaveResult?> Rejection(
        Guid key,
        long previousVersion,
        Guid menuId,
        IMenuItemCommands.MenuItemDraft draft,
        CancellationToken cancellationToken)
    {
        if (!await context.ActiveMenus().OfKey(menuId).AnyAsync(cancellationToken))
        {
            return new MenuItemSaveResult.UnknownMenu();
        }

        var hasParent = draft.ParentId.Match(() => false, _ => true);

        if (draft.Kind == MenuItemKind.User && (!BuiltInMenus.IsMain(menuId) || hasParent))
        {
            return new MenuItemSaveResult.KindNotAllowed();
        }

        if (hasParent && !await IsValidParent(key, previousVersion, menuId, draft, cancellationToken))
        {
            return new MenuItemSaveResult.InvalidParent();
        }

        return await AllRolesExist(draft.RoleIds, cancellationToken) ? null : new MenuItemSaveResult.UnknownRole();
    }

    private async Task<bool> IsValidParent(
        Guid key,
        long previousVersion,
        Guid menuId,
        IMenuItemCommands.MenuItemDraft draft,
        CancellationToken cancellationToken)
    {
        var parentId = draft.ParentId.GetOrElse(Guid.Empty);

        if (parentId == key)
        {
            return false;
        }

        var parent = (await context.LatestMenuItemsIn(DeletionState.Active)
                .OfKey(parentId)
                .ToRows()
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Head();

        var parentFits = parent.Match(
            () => false,
            row => row.MenuId == menuId && row.ParentId is null && row.Kind == MenuItemKind.Link);

        return parentFits && (previousVersion == 0 || (await ChildKeys(key, cancellationToken)).Count == 0);
    }

    private async Task<IReadOnlyCollection<Guid>> ChildKeys(Guid itemId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.MenuItems
            .LatestOf(context.MenuItems)
            .Where(item => EF.Property<Guid?>(item, MenuItemConfiguration.ParentIdProperty) == itemId)
            .Select(item => EF.Property<Guid>(item, VersionedColumns.Key))
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> AllRolesExist(IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        var wanted = roleIds.Distinct().ToList();

        return await context.Roles
            .Where(role => wanted.Contains(EF.Property<Guid>(role, "Id")))
            .CountAsync(cancellationToken) == wanted.Count;
    }

    private async Task<bool> ExistsInAnyState(Guid itemId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.MenuItems.OfKey(itemId).AnyAsync(cancellationToken);
    }
}
