using Behind.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class BuiltInMenusExtensionsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    [Fact]
    public async Task EnsureBuiltInMenus_CreatesTheMainMenuWithItsItemsAndRoles()
    {
        await _bed.EnsureBuiltInMenusAsync();

        var main = Assert.Single(await _bed.ResolveMenusAsync(None, BuiltInMenus.MainName));
        Assert.Equal(BuiltInMenus.Main.Id, main.Id);
        Assert.Equal(
            ["Index", "News", "Articles", "Galleries", "Downloads", "Wall of shame"],
            main.Items.Where(item => item.Kind == MenuItemKind.Link).Select(item => item.Title));
        Assert.Equal(9, main.Items.Count);
        Assert.DoesNotContain(main.Items, item => item.Title == "Administration");
    }

    [Fact]
    public async Task EnsureBuiltInMenus_UserItemsAreResolvedForTheirRolesOnly()
    {
        await _bed.EnsureBuiltInMenusAsync();
        var administrator = await _bed.SeedUserWithRolesAsync("admin@test.com", BuiltInAccess.Administrators.Id);
        var user = await _bed.SeedUserWithRolesAsync("user@test.com", BuiltInAccess.Users.Id);

        var forAdministrator = (await _bed.ResolveMenusAsync(administrator, BuiltInMenus.MainName))[0].Items;
        var forUser = (await _bed.ResolveMenusAsync(user, BuiltInMenus.MainName))[0].Items;

        Assert.Equal(
            ["Profile", "Session", "Sessions", "Administration"],
            forAdministrator.Where(item => item.Kind == MenuItemKind.User).Select(item => item.Title));
        Assert.Equal(
            ["Profile", "Session", "Sessions"],
            forUser.Where(item => item.Kind == MenuItemKind.User).Select(item => item.Title));
    }

    [Fact]
    public async Task EnsureBuiltInMenus_IsIdempotent()
    {
        await _bed.EnsureBuiltInMenusAsync();
        await _bed.EnsureBuiltInMenusAsync();

        await using var ctx = _bed.Read();
        Assert.Equal(1, await ctx.Menus.CountAsync());
        Assert.Equal(BuiltInMenus.Main.Items.Count, await ctx.MenuItems.CountAsync());
        Assert.Equal(
            BuiltInMenus.Main.Items.Sum(item => item.RoleIds.Count),
            await ctx.MenuItemRoles.CountAsync());
    }

    [Fact]
    public async Task EnsureBuiltInMenus_DoesNotBringBackAnItemAnAdministratorDeleted()
    {
        await _bed.EnsureBuiltInMenusAsync();
        var wallOfShame = BuiltInMenus.Main.Items.Single(item => item.Title == "Wall of shame");
        await _bed.WithMenuItemCommandsAsync(commands => commands.Delete(wallOfShame.Id));

        await _bed.EnsureBuiltInMenusAsync();

        var items = (await _bed.ResolveMenusAsync(None, BuiltInMenus.MainName))[0].Items;
        Assert.DoesNotContain(items, item => item.Id == wallOfShame.Id);
    }

    [Fact]
    public async Task EnsureBuiltInMenus_KeepsTheTitleAnAdministratorChanged()
    {
        await _bed.EnsureBuiltInMenusAsync();
        await _bed.WithMenuCommandsAsync(commands => commands.Update(
            BuiltInMenus.Main.Id,
            new IMenuCommands.MenuDraft(BuiltInMenus.MainName, "Navigation")));

        await _bed.EnsureBuiltInMenusAsync();

        Assert.Equal("Navigation", (await _bed.ResolveMenusAsync(None, BuiltInMenus.MainName))[0].Title);
    }
}
