using Behind.Core.Database;
using Behind.Core.Database.Queries;
using static Behind.Tests.Helpers.MenuTestBedExtensions;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class MenuItemQueriesTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<PagedResult<IMenuItemQueries.MenuItemListing>> PageAsync(
        Guid menuId,
        DeletionState state,
        Flipper? flipper = null,
        Option<string> search = default,
        Option<string> letter = default)
    {
        await using var ctx = _bed.Read();

        return await new MenuItemQueries(ctx).ListOfMenu(menuId, flipper ?? new Flipper(1, 10), search, letter, state);
    }

    private async Task<IReadOnlyList<IMenuItemQueries.MenuItemListing>> ListAsync(Guid menuId, DeletionState state) =>
        (await PageAsync(menuId, state)).Results.Match(() => [], items => items.ToList());

    [Fact]
    public async Task ListOfMenu_ReturnsTheLatestVersionOfEveryItemOfTheMenuWithItsRoles()
    {
        var editors = await _bed.SeedRoleAsync("Editors");
        var footer = await _bed.CreateMenuAsync("footer", "Footer");
        var sidebar = await _bed.CreateMenuAsync("sidebar", "Sidebar");
        var group = await _bed.CreateMenuItemAsync(footer, "Group", position: 20);
        var created = await _bed.SaveMenuItemAsync(null, footer, ItemDraft("Old", position: 10, parentId: group));
        await _bed.SaveMenuItemAsync(
            created.Key,
            footer,
            ItemDraft("New", "#/new", MenuTarget.NewTab, position: 10, parentId: group, roleIds: [editors]));
        await _bed.CreateMenuItemAsync(sidebar, "Elsewhere");

        var items = await ListAsync(footer, DeletionState.Active);

        Assert.Equal(["New", "Group"], items.Select(item => item.Title));
        var latest = items[0];
        Assert.Equal(new VersionedKey<Guid>(created.Key, 2), latest.Id);
        Assert.Equal(footer, latest.MenuId);
        Assert.Equal(Some(group), latest.ParentId);
        Assert.Equal(MenuTarget.NewTab, latest.Target);
        Assert.Equal([editors], latest.RoleIds);
        Assert.Empty(items[1].RoleIds);
    }

    [Fact]
    public async Task ListOfMenu_SeparatesActiveAndDeletedItems()
    {
        var footer = await _bed.CreateMenuAsync();
        await _bed.CreateMenuItemAsync(footer, "Stays");
        var gone = await _bed.CreateMenuItemAsync(footer, "Gone");
        await _bed.WithMenuItemCommandsAsync(commands => commands.Delete(gone));

        Assert.Equal(["Stays"], (await ListAsync(footer, DeletionState.Active)).Select(item => item.Title));
        Assert.Equal(["Gone"], (await ListAsync(footer, DeletionState.Deleted)).Select(item => item.Title));
    }

    [Fact]
    public async Task ListOfMenu_ListsTheItemsOfADeletedMenu()
    {
        var footer = await _bed.CreateMenuAsync();
        await _bed.CreateMenuItemAsync(footer, "Item");
        await _bed.WithMenuCommandsAsync(commands => commands.Delete(footer));

        Assert.Equal(["Item"], (await ListAsync(footer, DeletionState.Active)).Select(item => item.Title));
    }

    [Fact]
    public async Task ListOfMenu_UnknownMenu_ReturnsNothing()
    {
        Assert.Empty(await ListAsync(Guid.NewGuid(), DeletionState.Active));
    }

    [Fact]
    public async Task ListOfMenu_PagesTheItemsInPositionOrder()
    {
        var footer = await _bed.CreateMenuAsync();
        await _bed.CreateMenuItemAsync(footer, "Third", position: 30);
        await _bed.CreateMenuItemAsync(footer, "First", position: 10);
        await _bed.CreateMenuItemAsync(footer, "Second", position: 20);

        var second = await PageAsync(footer, DeletionState.Active, new Flipper(2, 2));

        Assert.Equal((3L, 2L, 2L), (second.Total, second.PageCount, second.Page));
        Assert.Equal(["Third"], second.Results.Match(() => [], items => items.Select(item => item.Title).ToList()));
        Assert.Equal(
            ["First", "Second"],
            (await PageAsync(footer, DeletionState.Active, new Flipper(1, 2))).Results.Match(() => [], items => items.Select(item => item.Title).ToList()));
    }

    [Fact]
    public async Task ListOfMenu_LetterAndSearchNarrowTheItems()
    {
        var footer = await _bed.CreateMenuAsync();
        await _bed.CreateMenuItemAsync(footer, "Docs", "#/docs");
        await _bed.CreateMenuItemAsync(footer, "Downloads", "https://example.com/files");
        await _bed.CreateMenuItemAsync(footer, "About", "#/about");

        Assert.Equal(2, (await PageAsync(footer, DeletionState.Active, letter: Some("D"))).Total);
        Assert.Equal(1, (await PageAsync(footer, DeletionState.Active, search: Some(" FILES "))).Total);
        Assert.Equal(2, (await PageAsync(footer, DeletionState.Active, search: Some("do"), letter: Some("D"))).Total);
        Assert.Equal(3, (await PageAsync(footer, DeletionState.Active, search: Some("  "))).Total);
    }

    [Fact]
    public async Task GetLetters_ReturnsTheFirstLettersOfTheItemsInTheRequestedState()
    {
        var footer = await _bed.CreateMenuAsync();
        await _bed.CreateMenuItemAsync(footer, "Docs", "#/docs");
        await _bed.CreateMenuItemAsync(footer, "About", "#/about");
        var gone = await _bed.CreateMenuItemAsync(footer, "Zebra", "#/zebra");
        await _bed.WithMenuItemCommandsAsync(commands => commands.Delete(gone));
        await using var ctx = _bed.Read();
        var queries = new MenuItemQueries(ctx);

        Assert.Equal(["A", "D"], await queries.GetLetters(footer, None, DeletionState.Active));
        Assert.Equal(["Z"], await queries.GetLetters(footer, None, DeletionState.Deleted));
        Assert.Equal(["D"], await queries.GetLetters(footer, Some("doc"), DeletionState.Active));
    }

    [Fact]
    public async Task ListParents_ReturnsTheActiveTopLevelLinksOfTheMenuOnly()
    {
        var footer = await _bed.CreateMenuAsync("footer", "Footer");
        var sidebar = await _bed.CreateMenuAsync("sidebar", "Sidebar");
        var group = await _bed.CreateMenuItemAsync(footer, "Group", position: 10);
        await _bed.CreateMenuItemAsync(footer, "Child", parentId: group);
        var gone = await _bed.CreateMenuItemAsync(footer, "Gone", position: 20);
        await _bed.WithMenuItemCommandsAsync(commands => commands.Delete(gone));
        await _bed.CreateMenuItemAsync(sidebar, "Elsewhere");

        await using var ctx = _bed.Read();
        var parents = await new MenuItemQueries(ctx).ListParents(footer);

        Assert.Equal(["Group"], parents.Select(parent => parent.Title));
    }
}
