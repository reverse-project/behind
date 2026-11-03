namespace Behind.Tests.Domain;

public class BuiltInMenusTests
{
    [Fact]
    public void Main_IsTheMainMenuWithTheCurrentTabs()
    {
        Assert.Equal("main", BuiltInMenus.Main.Name);
        Assert.Equal(
            ["Index", "News", "Articles", "Galleries", "Downloads", "Wall of shame"],
            BuiltInMenus.Main.Items.Where(item => item.Kind == MenuItemKind.Link).OrderBy(item => item.Position).Select(item => item.Title));
        Assert.Equal(
            ["#/", "#/news", "#/articles", "#/galleries", "#/downloads", "#/wall-of-shame"],
            BuiltInMenus.Main.Items.Where(item => item.Kind == MenuItemKind.Link).OrderBy(item => item.Position).Select(item => item.Url));
    }

    [Fact]
    public void Main_UserMenuItemsAreTheAccountPagesAndAdministration()
    {
        var userItems = BuiltInMenus.Main.Items.Where(item => item.Kind == MenuItemKind.User).OrderBy(item => item.Position).ToList();

        Assert.Equal(["Profile", "Session", "Sessions", "Administration"], userItems.Select(item => item.Title));
        Assert.Equal(["#/profile", "#/session", "#/sessions", "#/administration"], userItems.Select(item => item.Url));
    }

    [Fact]
    public void Main_AdministrationIsHiddenFromEveryoneButAdministratorsAndDevelopers()
    {
        var administration = BuiltInMenus.Main.Items.Single(item => item.Title == "Administration");
        var open = BuiltInMenus.Main.Items.Where(item => item.Title != "Administration");

        Assert.Equal([BuiltInAccess.Administrators.Id, BuiltInAccess.Developers.Id], administration.RoleIds);
        Assert.All(open, item => Assert.Empty(item.RoleIds));
    }

    [Fact]
    public void Main_ItemsAreSafeAndHaveUniqueIds()
    {
        Assert.All(BuiltInMenus.Main.Items, item => Assert.True(item.Url.IsSafeMenuUrl));
        Assert.Equal(BuiltInMenus.Main.Items.Count, BuiltInMenus.Main.Items.Select(item => item.Id).Distinct().Count());
        Assert.DoesNotContain(BuiltInMenus.Main.Id, BuiltInMenus.Main.Items.Select(item => item.Id));
    }

    [Fact]
    public void IsBuiltInAndIsMain_RecogniseTheMainMenuOnly()
    {
        Assert.True(BuiltInMenus.IsBuiltIn(BuiltInMenus.Main.Id));
        Assert.True(BuiltInMenus.IsMain(BuiltInMenus.Main.Id));
        Assert.False(BuiltInMenus.IsBuiltIn(Guid.NewGuid()));
        Assert.False(BuiltInMenus.IsMain(Guid.NewGuid()));
        Assert.Equal([BuiltInMenus.Main], BuiltInMenus.All);
    }

    [Fact]
    public void Ids_DoNotClashWithBuiltInGrantsRolesOrUsers()
    {
        Guid[] others =
        [
            ..BuiltInAccess.Grants.Select(grant => grant.Id),
            ..BuiltInAccess.Roles.Select(role => role.Id),
            DomainConstants.SystemRoleId,
            DomainConstants.GuestRoleId
        ];

        Assert.Empty(others.Intersect(BuiltInMenus.Main.Items.Select(item => item.Id).Append(BuiltInMenus.Main.Id)));
    }
}
