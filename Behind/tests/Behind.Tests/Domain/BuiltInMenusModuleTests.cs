namespace Behind.Tests.Domain;

public class BuiltInMenusModuleTests
{
    [Fact]
    public void For_WithoutModulesIsTheCoreMenus()
    {
        Assert.Equal(BuiltInMenus.Main.Items, Assert.Single(BuiltInMenus.For(ModuleCatalog.Empty)).Items);
    }

    [Fact]
    public void For_AppendsTheModuleItemsToTheMainMenu()
    {
        var main = Assert.Single(BuiltInMenus.For(NotesModule.Catalog));

        Assert.Equal(BuiltInMenus.Main.Id, main.Id);
        Assert.Equal(BuiltInMenus.Main.Items.Count + 2, main.Items.Count);
        Assert.Contains(main.Items, item => item.Id == NotesModule.MenuItemId && item.Kind == MenuItemKind.Link && item.RoleIds.Count == 0);
    }

    [Fact]
    public void For_MapsTheAudienceOfAnItemToTheBuiltInRoles()
    {
        var items = BuiltInMenus.For(NotesModule.Catalog)[0].Items;
        var admin = items.Single(item => item.Id == NotesModule.AdminMenuItemId);

        Assert.Equal(MenuItemKind.User, admin.Kind);
        Assert.Equal([BuiltInAccess.Administrators.Id, BuiltInAccess.Developers.Id], admin.RoleIds);
    }

    [Fact]
    public void ToBuiltIn_MapsDeveloperItemsToTheDevelopersRole()
    {
        var item = new ModuleMenuItem(Guid.NewGuid(), "Internals", "#/internals", 5, ModuleMenuKind.User, ModuleAudience.Developer);

        Assert.Equal([BuiltInAccess.Developers.Id], item.ToBuiltIn().RoleIds);
    }
}
