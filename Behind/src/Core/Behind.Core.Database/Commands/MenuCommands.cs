using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class MenuCommands(AbstractCoreContext context) : IMenuCommands
{
    public Task<MenuSaveResult> Create(IMenuCommands.MenuDraft draft, CancellationToken cancellationToken = default) =>
        AddVersion(Guid.CreateVersion7(), 0, draft, cancellationToken);

    public async Task<MenuSaveResult> Update(
        Guid menuId,
        IMenuCommands.MenuDraft draft,
        CancellationToken cancellationToken = default)
    {
        var latest = (await context.ActiveMenus()
                .OfKey(menuId)
                .OrderByDescending(menu => EF.Property<long>(menu, VersionedColumns.Version))
                .Select(menu => new { Version = EF.Property<long>(menu, VersionedColumns.Version), menu.Name })
                .Take(1)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Head();

        return await latest.Match(
            () => Task.FromResult<MenuSaveResult>(new MenuSaveResult.NotFound()),
            row => BuiltInMenus.IsBuiltIn(menuId) && row.Name != draft.Name
                ? Task.FromResult<MenuSaveResult>(new MenuSaveResult.BuiltIn())
                : AddVersion(menuId, row.Version, draft, cancellationToken));
    }

    public async Task<MenuRemovalResult> Delete(Guid menuId, CancellationToken cancellationToken = default)
    {
        if (BuiltInMenus.IsBuiltIn(menuId))
        {
            return MenuRemovalResult.BuiltIn;
        }

        var deleted = await context.MarkSoftDeletedAsync(context.Menus.OfKey(menuId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return deleted > 0 ? MenuRemovalResult.Removed : MenuRemovalResult.NotFound;
    }

    public async Task<MenuSaveResult> Restore(Guid menuId, CancellationToken cancellationToken = default)
    {
        if (!await context.HasDeletedRowsAsync(context.Menus.OfKey(menuId), cancellationToken))
        {
            return new MenuSaveResult.NotFound();
        }

        var latest = await Latest(menuId, cancellationToken);

        if (await IsNameTaken(menuId, latest.Name, cancellationToken))
        {
            return new MenuSaveResult.NameTaken();
        }

        await context.MarkRestoredAsync(context.Menus.OfKey(menuId), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new MenuSaveResult.Saved(new VersionedKey<Guid>(menuId, latest.Version));
    }

    public async Task<HardDeleteResult> HardDelete(Guid menuId, CancellationToken cancellationToken = default)
    {
        if (BuiltInMenus.IsBuiltIn(menuId))
        {
            return HardDeleteResult.Rejected;
        }

        var deleted = await context.MarkHardDeletedAsync(context.Menus.OfKey(menuId), cancellationToken);

        if (deleted == 0)
        {
            return HardDeleteResult.NotFound;
        }

        using (context.SoftDelete.IncludeDeleted())
        {
            var items = await context.MenuItems
                .Where(item => EF.Property<Guid>(item, MenuItemConfiguration.MenuIdProperty) == menuId)
                .ToListAsync(cancellationToken);

            items.ForEach(context.HardDelete);
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
        Guid menuId,
        IMenuCommands.MenuDraft draft,
        CancellationToken cancellationToken = default) =>
        !await ExistsInAnyState(menuId, cancellationToken)
        && await AddVersion(menuId, 0, draft, cancellationToken) is MenuSaveResult.Saved;

    private async Task<bool> ExistsInAnyState(Guid menuId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.Menus.OfKey(menuId).AnyAsync(cancellationToken);
    }

    private async Task<MenuSaveResult> AddVersion(
        Guid key,
        long previousVersion,
        IMenuCommands.MenuDraft draft,
        CancellationToken cancellationToken)
    {
        if (await IsNameTaken(key, draft.Name, cancellationToken))
        {
            return new MenuSaveResult.NameTaken();
        }

        var id = new VersionedKey<Guid>(key, previousVersion + 1);
        var menu = new Menu(draft.Name, draft.Title);

        await context.Menus.AddAsync(menu, cancellationToken);
        context.Entry(menu).StampVersionedKey(id.Key, id.Version);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new MenuSaveResult.VersionConflict();
        }

        return new MenuSaveResult.Saved(id);
    }

    private async Task<(long Version, string Name)> Latest(Guid menuId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        var latest = await context.Menus
            .OfKey(menuId)
            .OrderByDescending(menu => EF.Property<long>(menu, VersionedColumns.Version))
            .Select(menu => new { Version = EF.Property<long>(menu, VersionedColumns.Version), menu.Name })
            .FirstAsync(cancellationToken);

        return (latest.Version, latest.Name);
    }

    private async Task<bool> IsNameTaken(Guid menuId, string name, CancellationToken cancellationToken)
    {
        var lowered = name.ToLowerInvariant();

        return await context.ActiveMenus()
            .LatestOf(context.Menus)
            .AnyAsync(
                menu => menu.Name.ToLower() == lowered && EF.Property<Guid>(menu, VersionedColumns.Key) != menuId,
                cancellationToken);
    }
}
