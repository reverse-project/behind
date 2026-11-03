using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Commands;
using Microsoft.EntityFrameworkCore;
using static Behind.Tests.Helpers.MenuTestBedExtensions;

namespace Behind.Tests.CoreDatabase;

public sealed class MenuItemCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<IReadOnlyList<(long Version, string Title, string Url, Guid? Parent, MenuTarget Target, MenuItemKind Kind, int Position)>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();
        var rows = await ctx.MenuItems
            .OfKey(key)
            .OrderBy(item => EF.Property<long>(item, VersionedColumns.Version))
            .Select(item => new
            {
                Version = EF.Property<long>(item, VersionedColumns.Version),
                item.Title,
                item.Url,
                Parent = EF.Property<Guid?>(item, MenuItemConfiguration.ParentIdProperty),
                item.Target,
                item.Kind,
                item.Position
            })
            .ToListAsync();

        return rows.Select(row => (row.Version, row.Title, row.Url, row.Parent, row.Target, row.Kind, row.Position)).ToList();
    }

    private async Task<IReadOnlyList<Guid>> RolesOfAsync(VersionedKey<Guid> id)
    {
        await using var ctx = _bed.Read();

        return await ctx.MenuItemRoles
            .Where(role => EF.Property<Guid>(role, MenuItemConfiguration.RolesItemKeyProperty) == id.Key
                           && EF.Property<long>(role, MenuItemConfiguration.RolesItemVersionProperty) == id.Version)
            .Select(role => EF.Property<Guid>(role, MenuItemConfiguration.RolesRoleIdProperty))
            .ToListAsync();
    }

    [Fact]
    public async Task Create_StoresVersionOneWithTheTargetPositionAndTheRoles()
    {
        var roleId = await _bed.SeedRoleAsync("Editors");
        var menuId = await _bed.CreateMenuAsync();

        var id = await _bed.SaveMenuItemAsync(
            null,
            menuId,
            ItemDraft("Docs", "https://example.com", MenuTarget.NewTab, MenuItemKind.Link, 30, roleIds: [roleId]));

        Assert.Equal(
            [(1L, "Docs", "https://example.com", (Guid?)null, MenuTarget.NewTab, MenuItemKind.Link, 30)],
            await VersionsAsync(id.Key));
        Assert.Equal([roleId], await RolesOfAsync(id));
        Assert.Equal(["Created"], await _bed.ActionsAsync("MenuItem", id.Key));
    }

    [Fact]
    public async Task Create_DuplicateRoleIds_AreStoredOnce()
    {
        var roleId = await _bed.SeedRoleAsync("Editors");
        var menuId = await _bed.CreateMenuAsync();

        var id = await _bed.SaveMenuItemAsync(null, menuId, ItemDraft(roleIds: [roleId, roleId]));

        Assert.Equal([roleId], await RolesOfAsync(id));
    }

    [Fact]
    public async Task Create_UnknownMenu_IsRejected()
    {
        Assert.IsType<MenuItemSaveResult.UnknownMenu>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Create(Guid.NewGuid(), ItemDraft())));
    }

    [Fact]
    public async Task Create_InADeletedMenu_IsRejected()
    {
        var menuId = await _bed.CreateMenuAsync();
        await _bed.WithMenuCommandsAsync(commands => commands.Delete(menuId));

        Assert.IsType<MenuItemSaveResult.UnknownMenu>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Create(menuId, ItemDraft())));
    }

    [Fact]
    public async Task Create_UnknownRole_IsRejected()
    {
        var menuId = await _bed.CreateMenuAsync();

        Assert.IsType<MenuItemSaveResult.UnknownRole>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Create(menuId, ItemDraft(roleIds: [Guid.NewGuid()]))));
    }

    [Fact]
    public async Task Create_DeletedRole_IsRejected()
    {
        await _bed.EnsureBuiltInMenusAsync();
        var roleId = await _bed.SeedRoleAsync("Editors");
        await using (var ctx = _bed.Write())
        {
            await new RoleCommands(ctx).Delete(roleId);
        }

        var menuId = await _bed.CreateMenuAsync();

        Assert.IsType<MenuItemSaveResult.UnknownRole>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Create(menuId, ItemDraft(roleIds: [roleId]))));
    }

    [Fact]
    public async Task Create_UserItemInTheMainMenu_IsAllowed()
    {
        await _bed.EnsureBuiltInMenusAsync();

        Assert.IsType<MenuItemSaveResult.Saved>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Create(
                BuiltInMenus.Main.Id,
                ItemDraft("Billing", "#/billing", kind: MenuItemKind.User))));
    }

    [Fact]
    public async Task Create_UserItemInAnotherMenu_IsRejected()
    {
        var menuId = await _bed.CreateMenuAsync();

        Assert.IsType<MenuItemSaveResult.KindNotAllowed>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Create(
                menuId,
                ItemDraft(kind: MenuItemKind.User))));
    }

    [Fact]
    public async Task Create_UserItemWithAParent_IsRejected()
    {
        await _bed.EnsureBuiltInMenusAsync();
        var parent = await _bed.CreateMenuItemAsync(BuiltInMenus.Main.Id, "Group");

        Assert.IsType<MenuItemSaveResult.KindNotAllowed>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Create(
                BuiltInMenus.Main.Id,
                ItemDraft(kind: MenuItemKind.User, parentId: parent))));
    }

    [Fact]
    public async Task Create_SubItem_StoresTheParent()
    {
        var menuId = await _bed.CreateMenuAsync();
        var parent = await _bed.CreateMenuItemAsync(menuId, "Group");

        var child = await _bed.CreateMenuItemAsync(menuId, "Child", parentId: parent);

        Assert.Equal(parent, Assert.Single(await VersionsAsync(child)).Parent);
    }

    [Fact]
    public async Task Create_ParentOfAnotherMenu_IsRejected()
    {
        var menuId = await _bed.CreateMenuAsync("footer");
        var otherMenu = await _bed.CreateMenuAsync("sidebar", "Sidebar");
        var parent = await _bed.CreateMenuItemAsync(otherMenu, "Group");

        Assert.IsType<MenuItemSaveResult.InvalidParent>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Create(menuId, ItemDraft(parentId: parent))));
    }

    [Fact]
    public async Task Create_UnknownParent_IsRejected()
    {
        var menuId = await _bed.CreateMenuAsync();

        Assert.IsType<MenuItemSaveResult.InvalidParent>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Create(
                menuId,
                ItemDraft(parentId: Guid.NewGuid()))));
    }

    [Fact]
    public async Task Create_ParentThatIsASubItemItself_IsRejected()
    {
        var menuId = await _bed.CreateMenuAsync();
        var parent = await _bed.CreateMenuItemAsync(menuId, "Group");
        var child = await _bed.CreateMenuItemAsync(menuId, "Child", parentId: parent);

        Assert.IsType<MenuItemSaveResult.InvalidParent>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Create(menuId, ItemDraft(parentId: child))));
    }

    [Fact]
    public async Task Create_DeletedParent_IsRejected()
    {
        var menuId = await _bed.CreateMenuAsync();
        var parent = await _bed.CreateMenuItemAsync(menuId, "Group");
        await _bed.WithMenuItemCommandsAsync(commands => commands.Delete(parent));

        Assert.IsType<MenuItemSaveResult.InvalidParent>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Create(menuId, ItemDraft(parentId: parent))));
    }

    [Fact]
    public async Task Update_InsertsTheNextVersionWithItsOwnRolesAndKeepsTheOldOne()
    {
        var first = await _bed.SeedRoleAsync("Editors");
        var second = await _bed.SeedRoleAsync("Readers");
        var menuId = await _bed.CreateMenuAsync();
        var created = await _bed.SaveMenuItemAsync(null, menuId, ItemDraft("Docs", roleIds: [first]));

        var updated = await _bed.SaveMenuItemAsync(
            created.Key,
            menuId,
            ItemDraft("Documentation", "#/documentation", MenuTarget.NewWindow, roleIds: [second]));

        Assert.Equal(new VersionedKey<Guid>(created.Key, 2), updated);
        Assert.Equal(
            ["Docs", "Documentation"],
            (await VersionsAsync(created.Key)).Select(version => version.Title));
        Assert.Equal([first], await RolesOfAsync(created));
        Assert.Equal([second], await RolesOfAsync(updated));
    }

    [Fact]
    public async Task Update_UnknownItem_IsNotFound()
    {
        Assert.IsType<MenuItemSaveResult.NotFound>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Update(Guid.NewGuid(), ItemDraft())));
    }

    [Fact]
    public async Task Update_ItemAsItsOwnParent_IsRejected()
    {
        var menuId = await _bed.CreateMenuAsync();
        var item = await _bed.CreateMenuItemAsync(menuId, "Group");

        Assert.IsType<MenuItemSaveResult.InvalidParent>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Update(item, ItemDraft(parentId: item))));
    }

    [Fact]
    public async Task Update_ItemWithSubItems_CannotBecomeASubItem()
    {
        var menuId = await _bed.CreateMenuAsync();
        var group = await _bed.CreateMenuItemAsync(menuId, "Group");
        await _bed.CreateMenuItemAsync(menuId, "Child", parentId: group);
        var other = await _bed.CreateMenuItemAsync(menuId, "Other");

        Assert.IsType<MenuItemSaveResult.InvalidParent>(
            await _bed.WithMenuItemCommandsAsync(commands => commands.Update(group, ItemDraft(parentId: other))));
    }

    [Fact]
    public async Task Update_SubItem_CanMoveToTheTopLevel()
    {
        var menuId = await _bed.CreateMenuAsync();
        var group = await _bed.CreateMenuItemAsync(menuId, "Group");
        var child = await _bed.CreateMenuItemAsync(menuId, "Child", parentId: group);

        await _bed.SaveMenuItemAsync(child, menuId, ItemDraft("Child"));

        Assert.Equal([group, (Guid?)null], (await VersionsAsync(child)).Select(version => version.Parent));
    }

    [Fact]
    public async Task Delete_SoftDeletesEveryVersion()
    {
        var menuId = await _bed.CreateMenuAsync();
        var created = await _bed.SaveMenuItemAsync(null, menuId, ItemDraft());
        await _bed.SaveMenuItemAsync(created.Key, menuId, ItemDraft("Docs 2"));

        Assert.True(await _bed.WithMenuItemCommandsAsync(commands => commands.Delete(created.Key)));

        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("MenuItem", created.Key));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("MenuItem", created.Key, 2));
        Assert.False(await _bed.WithMenuItemCommandsAsync(commands => commands.Delete(Guid.NewGuid())));
    }

    [Fact]
    public async Task Restore_BringsADeletedItemBackAndKeepsItsRoles()
    {
        var roleId = await _bed.SeedRoleAsync("Editors");
        var menuId = await _bed.CreateMenuAsync();
        var created = await _bed.SaveMenuItemAsync(null, menuId, ItemDraft(roleIds: [roleId]));
        await _bed.WithMenuItemCommandsAsync(commands => commands.Delete(created.Key));

        Assert.True(await _bed.WithMenuItemCommandsAsync(commands => commands.Restore(created.Key)));

        Assert.Equal(["Created", "Deleted", "Restored"], await _bed.ActionsAsync("MenuItem", created.Key));
        Assert.Equal([roleId], await RolesOfAsync(created));
    }

    [Fact]
    public async Task Restore_ItemThatIsNotDeleted_ChangesNothing()
    {
        var menuId = await _bed.CreateMenuAsync();
        var item = await _bed.CreateMenuItemAsync(menuId);

        Assert.False(await _bed.WithMenuItemCommandsAsync(commands => commands.Restore(item)));
    }

    [Fact]
    public async Task HardDelete_RemovesADeletedItemItsSubItemsAndTheirRoles()
    {
        var roleId = await _bed.SeedRoleAsync("Editors");
        var menuId = await _bed.CreateMenuAsync();
        var group = await _bed.CreateMenuItemAsync(menuId, "Group", roleIds: [roleId]);
        var child = await _bed.CreateMenuItemAsync(menuId, "Child", parentId: group, roleIds: [roleId]);
        var sibling = await _bed.CreateMenuItemAsync(menuId, "Sibling");
        await _bed.WithMenuItemCommandsAsync(commands => commands.Delete(group));

        var result = await _bed.WithMenuItemCommandsAsync(commands => commands.HardDelete(group));

        Assert.Equal(HardDeleteResult.Deleted, result);
        await using var ctx = _bed.Read();
        using var scope = ctx.SoftDelete.IncludeDeleted();
        Assert.False(await ctx.MenuItems.OfKey(group).AnyAsync());
        Assert.False(await ctx.MenuItems.OfKey(child).AnyAsync());
        Assert.True(await ctx.MenuItems.OfKey(sibling).AnyAsync());
        Assert.Equal(0, await ctx.MenuItemRoles.CountAsync());
    }

    [Fact]
    public async Task HardDelete_ItemThatIsNotDeleted_IsNotFound()
    {
        var menuId = await _bed.CreateMenuAsync();
        var item = await _bed.CreateMenuItemAsync(menuId);

        Assert.Equal(HardDeleteResult.NotFound, await _bed.WithMenuItemCommandsAsync(commands => commands.HardDelete(item)));
    }

    [Fact]
    public async Task Ensure_CreatesTheItemOnceWithTheGivenId()
    {
        var menuId = await _bed.CreateMenuAsync();
        var itemId = Guid.NewGuid();

        Assert.True(await _bed.WithMenuItemCommandsAsync(commands => commands.Ensure(itemId, menuId, ItemDraft("Fixed"))));
        Assert.False(await _bed.WithMenuItemCommandsAsync(commands => commands.Ensure(itemId, menuId, ItemDraft("Fixed"))));
        Assert.Single(await VersionsAsync(itemId));
    }
}
