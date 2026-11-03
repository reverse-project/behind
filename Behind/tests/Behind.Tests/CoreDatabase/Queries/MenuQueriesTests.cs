using Behind.Core.Database;
using Behind.Core.Database.Queries;
using static Behind.Tests.Helpers.MenuTestBedExtensions;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class MenuQueriesTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<PagedResult<IMenuQueries.MenuListing>> ListAsync(
        Option<string> search = default,
        Option<string> letter = default,
        DeletionState state = DeletionState.Active,
        Flipper? flipper = null)
    {
        await using var ctx = _bed.Read();

        return await new MenuQueries(ctx).List(flipper ?? new Flipper(1, 10), search, letter, state);
    }

    private static IReadOnlyList<string> Titles(PagedResult<IMenuQueries.MenuListing> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Title).ToList());

    [Fact]
    public async Task List_ReturnsTheLatestVersionOfEveryMenuOrderedByTitleWithTheActiveItemCount()
    {
        var footer = await _bed.CreateMenuAsync("footer", "Footer");
        await _bed.WithMenuCommandsAsync(commands => commands.Update(footer, new IMenuCommands.MenuDraft("footer", "Page footer")));
        var sidebar = await _bed.CreateMenuAsync("sidebar", "Sidebar");
        await _bed.CreateMenuItemAsync(footer, "One");
        await _bed.CreateMenuItemAsync(footer, "Two");
        var deleted = await _bed.CreateMenuItemAsync(footer, "Three");
        await _bed.WithMenuItemCommandsAsync(commands => commands.Delete(deleted));

        var page = await ListAsync();

        Assert.Equal(2, page.Total);
        Assert.Equal(["Page footer", "Sidebar"], Titles(page));
        var listings = page.Results.GetOrElse([]).ToList();
        Assert.Equal(new VersionedKey<Guid>(footer, 2), listings[0].Id);
        Assert.Equal(2, listings[0].ItemCount);
        Assert.Equal(0, listings[1].ItemCount);
        Assert.Equal(sidebar, listings[1].Id.Key);
    }

    [Fact]
    public async Task List_SearchMatchesTheTitleAndTheName()
    {
        await _bed.CreateMenuAsync("footer", "Bottom links");
        await _bed.CreateMenuAsync("sidebar", "Sidebar");

        Assert.Equal(["Bottom links"], Titles(await ListAsync(Some("bottom"))));
        Assert.Equal(["Sidebar"], Titles(await ListAsync(Some("SIDE"))));
        Assert.Equal(2, (await ListAsync(Some("  "))).Total);
    }

    [Fact]
    public async Task List_LetterFiltersByTheFirstLetterOfTheTitle()
    {
        await _bed.CreateMenuAsync("footer", "Footer");
        await _bed.CreateMenuAsync("sidebar", "Sidebar");

        Assert.Equal(["Sidebar"], Titles(await ListAsync(letter: Some("S"))));
    }

    [Fact]
    public async Task List_PagesTheResults()
    {
        await _bed.CreateMenuAsync("a", "Alpha");
        await _bed.CreateMenuAsync("b", "Beta");
        await _bed.CreateMenuAsync("c", "Gamma");

        var page = await ListAsync(flipper: new Flipper(2, 2));

        Assert.Equal(3, page.Total);
        Assert.Equal(2, page.PageCount);
        Assert.Equal(["Gamma"], Titles(page));
    }

    [Fact]
    public async Task List_DeletedState_ReturnsOnlyDeletedMenus()
    {
        await _bed.CreateMenuAsync("footer", "Footer");
        var gone = await _bed.CreateMenuAsync("sidebar", "Sidebar");
        await _bed.WithMenuCommandsAsync(commands => commands.Delete(gone));

        Assert.Equal(["Sidebar"], Titles(await ListAsync(state: DeletionState.Deleted)));
        Assert.Equal(["Footer"], Titles(await ListAsync()));
    }

    [Fact]
    public async Task GetLetters_ReturnsTheFirstLettersOfTheMatchingTitles()
    {
        await _bed.CreateMenuAsync("footer", "Footer");
        await _bed.CreateMenuAsync("sidebar", "Sidebar");

        await using var ctx = _bed.Read();
        var letters = await new MenuQueries(ctx).GetLetters(None, DeletionState.Active);

        Assert.Equal(["F", "S"], letters);
    }

    [Fact]
    public async Task Find_ReturnsTheMenuInTheRequestedStateOnly()
    {
        var key = await _bed.CreateMenuAsync("footer", "Footer");
        await using (var ctx = _bed.Read())
        {
            Assert.Equal("Footer", TitleOf(await new MenuQueries(ctx).Find(key, DeletionState.Active)));
            Assert.Equal(None, await new MenuQueries(ctx).Find(key, DeletionState.Deleted));
        }

        await _bed.WithMenuCommandsAsync(commands => commands.Delete(key));

        await using var after = _bed.Read();
        Assert.Equal(None, await new MenuQueries(after).Find(key, DeletionState.Active));
        Assert.Equal("Footer", TitleOf(await new MenuQueries(after).Find(key, DeletionState.Deleted)));
        Assert.Equal(None, await new MenuQueries(after).Find(Guid.NewGuid(), DeletionState.Active));
    }

    private static string TitleOf(Option<IMenuQueries.MenuListing> found) =>
        found.Match(() => string.Empty, listing => listing.Title);

    [Fact]
    public async Task Resolve_ReturnsTheRequestedMenusWithTheirItemsInPositionOrder()
    {
        var footer = await _bed.CreateMenuAsync("footer", "Footer");
        var sidebar = await _bed.CreateMenuAsync("sidebar", "Sidebar");
        await _bed.CreateMenuAsync("other", "Other");
        await _bed.CreateMenuItemAsync(footer, "Second", "#/second", position: 20);
        await _bed.CreateMenuItemAsync(footer, "First", "#/first", position: 10);
        await _bed.CreateMenuItemAsync(sidebar, "Side", "#/side");

        var menus = await _bed.ResolveMenusAsync(None, "footer", "SIDEBAR", "missing");

        Assert.Equal(["Footer", "Sidebar"], menus.Select(menu => menu.Title));
        Assert.Equal(["First", "Second"], menus[0].Items.Select(item => item.Title));
        Assert.Equal(["Side"], menus[1].Items.Select(item => item.Title));
    }

    [Fact]
    public async Task Resolve_NoMatchingMenu_ReturnsNothing()
    {
        Assert.Empty(await _bed.ResolveMenusAsync(None, "missing"));
    }

    [Fact]
    public async Task Resolve_OmitsDeletedMenusAndDeletedItemsAndTheirSubItems()
    {
        var footer = await _bed.CreateMenuAsync("footer", "Footer");
        var gone = await _bed.CreateMenuAsync("sidebar", "Sidebar");
        var group = await _bed.CreateMenuItemAsync(footer, "Group", position: 10);
        await _bed.CreateMenuItemAsync(footer, "Child", parentId: group);
        await _bed.CreateMenuItemAsync(footer, "Stays", position: 20);
        await _bed.WithMenuItemCommandsAsync(commands => commands.Delete(group));
        await _bed.WithMenuCommandsAsync(commands => commands.Delete(gone));

        var menus = await _bed.ResolveMenusAsync(None, "footer", "sidebar");

        var menu = Assert.Single(menus);
        Assert.Equal(["Stays"], menu.Items.Select(item => item.Title));
    }

    [Fact]
    public async Task Resolve_UsesTheLatestVersionOfAnItem()
    {
        var footer = await _bed.CreateMenuAsync("footer", "Footer");
        var first = await _bed.SaveMenuItemAsync(null, footer, ItemDraft("Old", "#/old"));
        await _bed.SaveMenuItemAsync(first.Key, footer, ItemDraft("New", "#/new", MenuTarget.NewWindow));

        var item = Assert.Single(Assert.Single(await _bed.ResolveMenusAsync(None, "footer")).Items);

        Assert.Equal(
            (first.Key, "New", "#/new", MenuTarget.NewWindow, MenuItemKind.Link),
            (item.Id, item.Title, item.Url, item.Target, item.Kind));
        Assert.Empty(item.Children);
    }

    [Fact]
    public async Task Resolve_ItemsWithoutRoles_AreVisibleToEveryoneIncludingVisitors()
    {
        var footer = await _bed.CreateMenuAsync("footer", "Footer");
        await _bed.CreateMenuItemAsync(footer, "Open");
        var userId = await _bed.SeedUserWithRolesAsync("reader@test.com");

        Assert.Single((await _bed.ResolveMenusAsync(None, "footer"))[0].Items);
        Assert.Single((await _bed.ResolveMenusAsync(userId, "footer"))[0].Items);
    }

    [Fact]
    public async Task Resolve_ItemsWithRoles_AreVisibleOnlyToUsersHoldingOneOfTheRoles()
    {
        var editors = await _bed.SeedRoleAsync("Editors");
        var reviewers = await _bed.SeedRoleAsync("Reviewers");
        var footer = await _bed.CreateMenuAsync("footer", "Footer");
        await _bed.CreateMenuItemAsync(footer, "Editor tools", roleIds: [editors]);
        await _bed.CreateMenuItemAsync(footer, "Review tools", roleIds: [reviewers, editors]);
        var editor = await _bed.SeedUserWithRolesAsync("editor@test.com", editors);
        var reviewer = await _bed.SeedUserWithRolesAsync("reviewer@test.com", reviewers);
        var plain = await _bed.SeedUserWithRolesAsync("plain@test.com");

        Assert.Equal(["Editor tools", "Review tools"], TitlesOf(await _bed.ResolveMenusAsync(editor, "footer")));
        Assert.Equal(["Review tools"], TitlesOf(await _bed.ResolveMenusAsync(reviewer, "footer")));
        Assert.Empty(TitlesOf(await _bed.ResolveMenusAsync(plain, "footer")));
        Assert.Empty(TitlesOf(await _bed.ResolveMenusAsync(None, "footer")));
    }

    [Fact]
    public async Task Resolve_RolesOfAnOlderVersionAreIgnored()
    {
        var editors = await _bed.SeedRoleAsync("Editors");
        var footer = await _bed.CreateMenuAsync("footer", "Footer");
        var first = await _bed.SaveMenuItemAsync(null, footer, ItemDraft("Tools", roleIds: [editors]));
        await _bed.SaveMenuItemAsync(first.Key, footer, ItemDraft("Tools"));

        Assert.Single(TitlesOf(await _bed.ResolveMenusAsync(None, "footer")));
    }

    [Fact]
    public async Task Resolve_RoleOfASoftDeletedRoleNoLongerGrantsVisibility()
    {
        var editors = await _bed.SeedRoleAsync("Editors");
        var footer = await _bed.CreateMenuAsync("footer", "Footer");
        await _bed.CreateMenuItemAsync(footer, "Tools", roleIds: [editors]);
        var editor = await _bed.SeedUserWithRolesAsync("editor@test.com", editors);
        await using (var ctx = _bed.Write())
        {
            await new Behind.Core.Database.Commands.RoleCommands(ctx).Delete(editors);
        }

        Assert.Empty(TitlesOf(await _bed.ResolveMenusAsync(editor, "footer")));
    }

    [Fact]
    public async Task Resolve_SubItemsAreNestedUnderTheirVisibleParentOnly()
    {
        var editors = await _bed.SeedRoleAsync("Editors");
        var footer = await _bed.CreateMenuAsync("footer", "Footer");
        var group = await _bed.CreateMenuItemAsync(footer, "Group", position: 10);
        await _bed.CreateMenuItemAsync(footer, "Public child", parentId: group, position: 20);
        await _bed.CreateMenuItemAsync(footer, "Editor child", parentId: group, position: 10, roleIds: [editors]);
        var hidden = await _bed.CreateMenuItemAsync(footer, "Hidden group", position: 20, roleIds: [editors]);
        await _bed.CreateMenuItemAsync(footer, "Child of hidden", parentId: hidden);
        var editor = await _bed.SeedUserWithRolesAsync("editor@test.com", editors);

        var asVisitor = (await _bed.ResolveMenusAsync(None, "footer"))[0].Items;
        var asEditor = (await _bed.ResolveMenusAsync(editor, "footer"))[0].Items;

        Assert.Equal(["Group"], asVisitor.Select(item => item.Title));
        Assert.Equal(["Public child"], asVisitor[0].Children.Select(child => child.Title));
        Assert.Equal(["Group", "Hidden group"], asEditor.Select(item => item.Title));
        Assert.Equal(["Editor child", "Public child"], asEditor[0].Children.Select(child => child.Title));
        Assert.Equal(["Child of hidden"], asEditor[1].Children.Select(child => child.Title));
    }

    private static IReadOnlyList<string> TitlesOf(IReadOnlyList<IMenuQueries.MenuView> menus) =>
        menus.SelectMany(menu => menu.Items).Select(item => item.Title).ToList();
}
