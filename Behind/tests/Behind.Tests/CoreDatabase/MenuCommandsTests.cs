using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Commands;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class MenuCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static IMenuCommands.MenuDraft Draft(string name = "footer", string title = "Footer") => new(name, title);

    private async Task<IReadOnlyList<(long Version, string Name, string Title)>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();
        var rows = await ctx.Menus
            .OfKey(key)
            .OrderBy(menu => EF.Property<long>(menu, VersionedColumns.Version))
            .Select(menu => new { Version = EF.Property<long>(menu, VersionedColumns.Version), menu.Name, menu.Title })
            .ToListAsync();

        return rows.Select(row => (row.Version, row.Name, row.Title)).ToList();
    }

    [Fact]
    public async Task Create_StoresVersionOneAndAuditsTheCreation()
    {
        var key = await _bed.CreateMenuAsync("footer", "Footer");

        Assert.Equal([(1L, "footer", "Footer")], await VersionsAsync(key));
        Assert.Equal(["Created"], await _bed.ActionsAsync("Menu", key));
    }

    [Fact]
    public async Task Create_DuplicateNameIgnoringCase_IsRejected()
    {
        await _bed.CreateMenuAsync("footer");

        Assert.IsType<MenuSaveResult.NameTaken>(
            await _bed.WithMenuCommandsAsync(commands => commands.Create(Draft("FOOTER", "Other"))));
    }

    [Fact]
    public async Task Update_InsertsTheNextVersionAndKeepsTheOldOne()
    {
        var key = await _bed.CreateMenuAsync("footer", "Footer");

        var result = await _bed.WithMenuCommandsAsync(commands => commands.Update(key, Draft("bottom", "Bottom")));

        Assert.Equal(new VersionedKey<Guid>(key, 2), Assert.IsType<MenuSaveResult.Saved>(result).Id);
        Assert.Equal([(1L, "footer", "Footer"), (2L, "bottom", "Bottom")], await VersionsAsync(key));
    }

    [Fact]
    public async Task Update_KeepingItsOwnName_IsAllowed()
    {
        var key = await _bed.CreateMenuAsync("footer", "Footer");

        Assert.IsType<MenuSaveResult.Saved>(
            await _bed.WithMenuCommandsAsync(commands => commands.Update(key, Draft("footer", "Page footer"))));
    }

    [Fact]
    public async Task Update_NameOfAnotherMenu_IsRejected()
    {
        await _bed.CreateMenuAsync("footer");
        var other = await _bed.CreateMenuAsync("sidebar", "Sidebar");

        Assert.IsType<MenuSaveResult.NameTaken>(
            await _bed.WithMenuCommandsAsync(commands => commands.Update(other, Draft("footer"))));
    }

    [Fact]
    public async Task Update_UnknownMenu_IsNotFound()
    {
        Assert.IsType<MenuSaveResult.NotFound>(
            await _bed.WithMenuCommandsAsync(commands => commands.Update(Guid.NewGuid(), Draft())));
    }

    [Fact]
    public async Task Update_RenamingTheBuiltInMenu_IsRejectedButRetitlingIsAllowed()
    {
        await _bed.EnsureBuiltInMenusAsync();
        var main = BuiltInMenus.Main;

        Assert.IsType<MenuSaveResult.BuiltIn>(
            await _bed.WithMenuCommandsAsync(commands => commands.Update(main.Id, Draft("renamed", main.Title))));
        Assert.IsType<MenuSaveResult.Saved>(
            await _bed.WithMenuCommandsAsync(commands => commands.Update(main.Id, Draft(main.Name, "Navigation"))));
    }

    [Fact]
    public async Task Delete_SoftDeletesEveryVersionAndFreesTheName()
    {
        var key = await _bed.CreateMenuAsync("footer");
        await _bed.WithMenuCommandsAsync(commands => commands.Update(key, Draft("footer", "Footer 2")));

        var removed = await _bed.WithMenuCommandsAsync(commands => commands.Delete(key));

        Assert.Equal(MenuRemovalResult.Removed, removed);
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("Menu", key));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("Menu", key, 2));
        Assert.IsType<MenuSaveResult.Saved>(
            await _bed.WithMenuCommandsAsync(commands => commands.Create(Draft("footer", "New footer"))));
    }

    [Fact]
    public async Task Delete_UnknownMenu_IsNotFound()
    {
        Assert.Equal(
            MenuRemovalResult.NotFound,
            await _bed.WithMenuCommandsAsync(commands => commands.Delete(Guid.NewGuid())));
    }

    [Fact]
    public async Task Delete_TheBuiltInMenu_IsRefused()
    {
        await _bed.EnsureBuiltInMenusAsync();

        Assert.Equal(
            MenuRemovalResult.BuiltIn,
            await _bed.WithMenuCommandsAsync(commands => commands.Delete(BuiltInMenus.Main.Id)));
    }

    [Fact]
    public async Task Restore_BringsTheMenuBack()
    {
        var key = await _bed.CreateMenuAsync("footer");
        await _bed.WithMenuCommandsAsync(commands => commands.Delete(key));

        var result = await _bed.WithMenuCommandsAsync(commands => commands.Restore(key));

        Assert.Equal(new VersionedKey<Guid>(key, 1), Assert.IsType<MenuSaveResult.Saved>(result).Id);
        Assert.Equal(["Created", "Deleted", "Restored"], await _bed.ActionsAsync("Menu", key));
    }

    [Fact]
    public async Task Restore_MenuThatIsNotDeleted_IsNotFound()
    {
        var key = await _bed.CreateMenuAsync("footer");

        Assert.IsType<MenuSaveResult.NotFound>(await _bed.WithMenuCommandsAsync(commands => commands.Restore(key)));
    }

    [Fact]
    public async Task Restore_WhileAnotherMenuUsesTheName_IsRejected()
    {
        var key = await _bed.CreateMenuAsync("footer");
        await _bed.WithMenuCommandsAsync(commands => commands.Delete(key));
        await _bed.CreateMenuAsync("footer", "Replacement");

        Assert.IsType<MenuSaveResult.NameTaken>(await _bed.WithMenuCommandsAsync(commands => commands.Restore(key)));
    }

    [Fact]
    public async Task HardDelete_RemovesADeletedMenuTogetherWithItsItemsAndTheirRoles()
    {
        var roleId = await _bed.SeedRoleAsync("Editors");
        var key = await _bed.CreateMenuAsync("footer");
        var itemId = await _bed.CreateMenuItemAsync(key, roleIds: [roleId]);
        await _bed.WithMenuCommandsAsync(commands => commands.Delete(key));

        var result = await _bed.WithMenuCommandsAsync(commands => commands.HardDelete(key));

        Assert.Equal(HardDeleteResult.Deleted, result);
        await using var ctx = _bed.Read();
        using var scope = ctx.SoftDelete.IncludeDeleted();
        Assert.False(await ctx.Menus.OfKey(key).AnyAsync());
        Assert.False(await ctx.MenuItems.OfKey(itemId).AnyAsync());
        Assert.Equal(0, await ctx.MenuItemRoles.CountAsync());
    }

    [Fact]
    public async Task HardDelete_MenuThatIsNotDeleted_IsNotFound()
    {
        var key = await _bed.CreateMenuAsync("footer");

        Assert.Equal(HardDeleteResult.NotFound, await _bed.WithMenuCommandsAsync(commands => commands.HardDelete(key)));
    }

    [Fact]
    public async Task HardDelete_TheBuiltInMenu_IsRejected()
    {
        await _bed.EnsureBuiltInMenusAsync();

        Assert.Equal(
            HardDeleteResult.Rejected,
            await _bed.WithMenuCommandsAsync(commands => commands.HardDelete(BuiltInMenus.Main.Id)));
    }

    [Fact]
    public async Task Ensure_CreatesTheMenuOnceWithTheGivenIdAndNeverAgainEvenAfterDeletion()
    {
        var id = Guid.NewGuid();

        Assert.True(await _bed.WithMenuCommandsAsync(commands => commands.Ensure(id, Draft("fixed", "Fixed"))));
        Assert.False(await _bed.WithMenuCommandsAsync(commands => commands.Ensure(id, Draft("fixed", "Fixed"))));
        Assert.Equal([(1L, "fixed", "Fixed")], await VersionsAsync(id));

        await _bed.WithMenuCommandsAsync(commands => commands.Delete(id));

        Assert.False(await _bed.WithMenuCommandsAsync(commands => commands.Ensure(id, Draft("fixed", "Fixed"))));
    }
}
