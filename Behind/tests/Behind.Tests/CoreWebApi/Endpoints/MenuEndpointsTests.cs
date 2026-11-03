using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class MenuEndpointsTests
{
    private static readonly Guid MenuId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ItemId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid RoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static IMenuQueries.MenuListing Listing(string name = "footer", string title = "Footer", long items = 2) =>
        new(new VersionedKey<Guid>(MenuId, 1), name, title, items);

    private static IMenuItemQueries.MenuItemListing ItemListing(Option<Guid> parent = default) =>
        new(
            new VersionedKey<Guid>(ItemId, 1),
            MenuId,
            parent,
            "Docs",
            "#/docs",
            MenuTarget.NewTab,
            MenuItemKind.Link,
            10,
            [RoleId]);

    private static PagedResult<IMenuQueries.MenuListing> Page(params IMenuQueries.MenuListing[] listings) =>
        new(
            listings.Length,
            1,
            1,
            10,
            listings.Length > 0 ? Some<IEnumerable<IMenuQueries.MenuListing>>(listings) : None);

    private static IMenuItemQueries ItemQueries(
        DeletionState state = DeletionState.Active,
        params IMenuItemQueries.MenuItemListing[] listings)
    {
        var queries = Substitute.For<IMenuItemQueries>();
        queries.ListOfMenu(default, default!, default, default, default, default).ReturnsForAnyArgs(
            new PagedResult<IMenuItemQueries.MenuItemListing>(
                listings.Length,
                1,
                1,
                10,
                listings.Length > 0 ? Some<IEnumerable<IMenuItemQueries.MenuItemListing>>(listings) : None));
        queries.GetLetters(default, default, default, default).ReturnsForAnyArgs(["D"]);
        queries.ListParents(default, default).ReturnsForAnyArgs([ItemListing()]);

        return queries;
    }

    private static MenuEndpoints.SaveMenuRequest MenuRequest(string name = "Footer Links", string title = "  Footer ") =>
        new(name, title);

    private static MenuEndpoints.SaveMenuItemRequest ItemRequest(
        string title = " Docs ",
        string url = " #/docs ",
        MenuTarget target = MenuTarget.NewTab,
        MenuItemKind kind = MenuItemKind.Link,
        int position = 10,
        Guid? parent = null,
        IReadOnlyList<Guid>? roles = null) =>
        new(title, url, target, kind, position, parent, roles);

    private static IResult SavedResult => Results.Json(new NewsSavedResponse(MenuId, 1));

    [Fact]
    public async Task Resolve_NoName_IsABadRequest()
    {
        var queries = Substitute.For<IMenuQueries>();

        var result = await MenuEndpoints.Resolve(
            new DefaultHttpContext(),
            Substitute.For<ISessionQueries>(),
            queries,
            [" ", ""]);

        Assert.IsType<BadRequest>(result);
        await queries.DidNotReceiveWithAnyArgs().Resolve(default!, default, default);
    }

    [Fact]
    public async Task Resolve_NamesNotGiven_IsABadRequest()
    {
        Assert.IsType<BadRequest>(await MenuEndpoints.Resolve(
            new DefaultHttpContext(),
            Substitute.For<ISessionQueries>(),
            Substitute.For<IMenuQueries>()));
    }

    [Fact]
    public async Task Resolve_MoreMenusThanAllowed_IsABadRequest()
    {
        var names = Enumerable.Range(0, MenuEndpoints.MaxMenusPerRequest + 1).Select(index => $"menu-{index}").ToArray();

        Assert.IsType<BadRequest>(await MenuEndpoints.Resolve(
            new DefaultHttpContext(),
            Substitute.For<ISessionQueries>(),
            Substitute.For<IMenuQueries>(),
            names));
    }

    [Fact]
    public async Task Resolve_Visitor_AsksForTheMenusWithoutAViewerAndIsNotCached()
    {
        var queries = Substitute.For<IMenuQueries>();
        queries.Resolve(default!, default, default).ReturnsForAnyArgs([]);
        var context = new DefaultHttpContext();

        await MenuEndpoints.Resolve(context, Substitute.For<ISessionQueries>(), queries, ["main", " MAIN ", "footer"]);

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        await queries.Received(1).Resolve(
            Arg.Is<IReadOnlyCollection<string>>(names => names.SequenceEqual(new[] { "main", "footer" })),
            None,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_SignedInUser_AsksForTheMenusOfTheViewer()
    {
        var queries = Substitute.For<IMenuQueries>();
        queries.Resolve(default!, default, default).ReturnsForAnyArgs([]);

        await MenuEndpoints.Resolve(
            SessionTestHelper.SignedIn(SessionId),
            SessionTestHelper.SessionQueries(SessionId, UserId),
            queries,
            ["main"]);

        await queries.Received(1).Resolve(Arg.Any<IReadOnlyCollection<string>>(), Some(UserId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_ReturnsTheMenusWithTheirNestedItems()
    {
        var queries = Substitute.For<IMenuQueries>();
        var child = new IMenuQueries.MenuItemView(Guid.NewGuid(), "Child", "#/child", MenuTarget.NewWindow, MenuItemKind.Link, []);
        queries.Resolve(default!, default, default).ReturnsForAnyArgs(
        [
            new IMenuQueries.MenuView(
                MenuId,
                "main",
                "Main menu",
                [new IMenuQueries.MenuItemView(ItemId, "Group", "#/group", MenuTarget.CurrentPage, MenuItemKind.User, [child])])
        ]);

        var result = await MenuEndpoints.Resolve(
            new DefaultHttpContext(),
            Substitute.For<ISessionQueries>(),
            queries,
            ["main"]);

        var menu = Assert.Single(Assert.IsType<JsonHttpResult<MenusResponse>>(result).Value!.Menus);
        Assert.Equal(("main", "Main menu"), (menu.Name, menu.Title));
        var item = Assert.Single(menu.Items);
        Assert.Equal((MenuItemKind.User, "#/group"), (item.Kind, item.Url));
        var nested = Assert.Single(item.Children);
        Assert.Equal((child.Id, "Child", "#/child", MenuTarget.NewWindow, MenuItemKind.Link), (nested.Id, nested.Title, nested.Url, nested.Target, nested.Kind));
        Assert.Empty(nested.Children);
    }

    [Fact]
    public async Task Manage_ReturnsTheMenusWithLettersAndFlagsTheBuiltInOne()
    {
        var queries = Substitute.For<IMenuQueries>();
        var main = new IMenuQueries.MenuListing(new VersionedKey<Guid>(BuiltInMenus.Main.Id, 1), "main", "Main menu", 8);
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page(main, Listing()));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs(["F", "M"]);

        var result = await MenuEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService());

        var body = Assert.IsType<JsonHttpResult<MenuManageResponse>>(result).Value!;
        Assert.Equal(["F", "M"], body.Letters);
        Assert.Equal([true, false], body.Menus.Items.Select(menu => menu.IsBuiltIn));
        Assert.Equal(2, body.Menus.Items[1].ItemCount);
    }

    [Fact]
    public async Task Manage_PassesTheSearchLetterAndPaging()
    {
        var queries = Substitute.For<IMenuQueries>();
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page());
        queries.GetLetters(default, default, default).ReturnsForAnyArgs([]);

        await MenuEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService(), 2, 5, " foo ", "f");

        await queries.Received(1).List(new Flipper(2, 5), Some("foo"), Some("F"), DeletionState.Active, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manage_DeletedMenus_NeedTheDeletedRecordsGrant()
    {
        var result = await MenuEndpoints.Manage(
            new DefaultHttpContext(),
            Substitute.For<IMenuQueries>(),
            new FakeAuthorizationService(),
            deleted: true);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Manage_DeletedMenus_AreMarkedDeleted()
    {
        var queries = Substitute.For<IMenuQueries>();
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page(Listing()));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs([]);

        var result = await MenuEndpoints.Manage(
            new DefaultHttpContext(),
            queries,
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant),
            deleted: true);

        Assert.True(Assert.Single(Assert.IsType<JsonHttpResult<MenuManageResponse>>(result).Value!.Menus.Items).IsDeleted);
    }

    [Fact]
    public async Task ManageItems_ReturnsTheMenuItsItemsAndTheAvailableRoles()
    {
        var menus = Substitute.For<IMenuQueries>();
        menus.Find(MenuId, DeletionState.Active, Arg.Any<CancellationToken>()).Returns(Some(Listing()));
        var items = ItemQueries(DeletionState.Active, ItemListing(Some(Guid.Empty)));
        var roles = Substitute.For<IRoleQueries>();
        roles.ListRoles(Arg.Any<CancellationToken>()).Returns([new IRoleQueries.RoleResult(RoleId, new UserRole("Editors"))]);

        var result = await MenuEndpoints.ManageItems(
            MenuId,
            new DefaultHttpContext(),
            menus,
            items,
            roles,
            new FakeAuthorizationService());

        var body = Assert.IsType<JsonHttpResult<MenuItemsManageResponse>>(result).Value!;
        Assert.Equal(("Footer", false, false), (body.Menu.Title, body.Menu.IsBuiltIn, body.AllowsUserItems));
        var item = Assert.Single(body.Items.Items);
        Assert.Equal(["D"], body.Letters);
        Assert.Equal(new ManagedMenuParent(ItemId, "Docs"), Assert.Single(body.Parents));
        Assert.Equal(
            (ItemId, (Guid?)Guid.Empty, "Docs", "#/docs", MenuTarget.NewTab, MenuItemKind.Link, 10, false),
            (item.Id, item.ParentId, item.Title, item.Url, item.Target, item.Kind, item.Position, item.IsDeleted));
        Assert.Equal([RoleId], item.RoleIds);
        Assert.Equal(new MenuRoleItem(RoleId, "Editors"), Assert.Single(body.Roles));
    }

    [Fact]
    public async Task ManageItems_PassesThePagingSearchLetterAndState()
    {
        var menus = Substitute.For<IMenuQueries>();
        menus.Find(MenuId, DeletionState.Active, Arg.Any<CancellationToken>()).Returns(Some(Listing()));
        var items = ItemQueries();
        var roles = Substitute.For<IRoleQueries>();
        roles.ListRoles(Arg.Any<CancellationToken>()).Returns([]);

        await MenuEndpoints.ManageItems(
            MenuId, new DefaultHttpContext(), menus, items, roles, new FakeAuthorizationService(), 2, 5, " doc ", "d");

        await items.Received(1).ListOfMenu(
            MenuId, new Flipper(2, 5), Some("doc"), Some("D"), DeletionState.Active, Arg.Any<CancellationToken>());
        await items.Received(1).GetLetters(MenuId, Some("doc"), DeletionState.Active, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ManageItems_UnknownLetter_IsABadRequest()
    {
        var result = await MenuEndpoints.ManageItems(
            MenuId,
            new DefaultHttpContext(),
            Substitute.For<IMenuQueries>(),
            ItemQueries(),
            Substitute.For<IRoleQueries>(),
            new FakeAuthorizationService(),
            letter: "12");

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task ManageItems_TheMainMenu_AllowsUserItems()
    {
        var menus = Substitute.For<IMenuQueries>();
        menus.Find(BuiltInMenus.Main.Id, DeletionState.Active, Arg.Any<CancellationToken>()).Returns(
            Some(new IMenuQueries.MenuListing(new VersionedKey<Guid>(BuiltInMenus.Main.Id, 1), "main", "Main menu", 0)));
        var items = ItemQueries();
        var roles = Substitute.For<IRoleQueries>();
        roles.ListRoles(Arg.Any<CancellationToken>()).Returns([]);

        var result = await MenuEndpoints.ManageItems(
            BuiltInMenus.Main.Id,
            new DefaultHttpContext(),
            menus,
            items,
            roles,
            new FakeAuthorizationService());

        var body = Assert.IsType<JsonHttpResult<MenuItemsManageResponse>>(result).Value!;
        Assert.True(body.AllowsUserItems);
        Assert.True(body.Menu.IsBuiltIn);
    }

    [Fact]
    public async Task ManageItems_UnknownMenu_IsNotFound()
    {
        var menus = Substitute.For<IMenuQueries>();
        menus.Find(default, default, default).ReturnsForAnyArgs(None);

        var result = await MenuEndpoints.ManageItems(
            MenuId,
            new DefaultHttpContext(),
            menus,
            Substitute.For<IMenuItemQueries>(),
            Substitute.For<IRoleQueries>(),
            new FakeAuthorizationService());

        Assert.IsType<NotFound>(result);
        await menus.DidNotReceive().Find(MenuId, DeletionState.Deleted, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ManageItems_DeletedMenu_IsOnlyShownToThoseWhoMaySeeDeletedRecords()
    {
        var menus = Substitute.For<IMenuQueries>();
        menus.Find(MenuId, DeletionState.Active, Arg.Any<CancellationToken>()).Returns(None);
        menus.Find(MenuId, DeletionState.Deleted, Arg.Any<CancellationToken>()).Returns(Some(Listing()));
        var items = ItemQueries();
        var roles = Substitute.For<IRoleQueries>();
        roles.ListRoles(Arg.Any<CancellationToken>()).Returns([]);

        var hidden = await MenuEndpoints.ManageItems(
            MenuId, new DefaultHttpContext(), menus, items, roles, new FakeAuthorizationService());
        var shown = await MenuEndpoints.ManageItems(
            MenuId,
            new DefaultHttpContext(),
            menus,
            items,
            roles,
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant));

        Assert.IsType<NotFound>(hidden);
        Assert.True(Assert.IsType<JsonHttpResult<MenuItemsManageResponse>>(shown).Value!.Menu.IsDeleted);
    }

    [Fact]
    public async Task ManageItems_DeletedItems_NeedTheDeletedRecordsGrantAndAreMarkedDeleted()
    {
        var menus = Substitute.For<IMenuQueries>();
        menus.Find(MenuId, DeletionState.Active, Arg.Any<CancellationToken>()).Returns(Some(Listing()));
        var items = ItemQueries(DeletionState.Deleted, ItemListing());
        var roles = Substitute.For<IRoleQueries>();
        roles.ListRoles(Arg.Any<CancellationToken>()).Returns([]);

        var denied = await MenuEndpoints.ManageItems(
            MenuId, new DefaultHttpContext(), menus, items, roles, new FakeAuthorizationService(), deleted: true);
        var allowed = await MenuEndpoints.ManageItems(
            MenuId,
            new DefaultHttpContext(),
            menus,
            items,
            roles,
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant),
            deleted: true);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(denied).StatusCode);
        Assert.True(Assert.Single(Assert.IsType<JsonHttpResult<MenuItemsManageResponse>>(allowed).Value!.Items.Items).IsDeleted);
    }

    [Fact]
    public async Task Create_StoresTheSlugOfTheNameAndTheTrimmedTitle()
    {
        var commands = Substitute.For<IMenuCommands>();
        commands.Create(default!, default).ReturnsForAnyArgs(new MenuSaveResult.Saved(new VersionedKey<Guid>(MenuId, 1)));

        var result = await MenuEndpoints.Create(MenuRequest(), new DefaultHttpContext(), commands);

        Assert.Equal(new NewsSavedResponse(MenuId, 1), Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await commands.Received(1).Create(new IMenuCommands.MenuDraft("footer-links", "Footer"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "Footer")]
    [InlineData("!!!", "Footer")]
    [InlineData("footer", "   ")]
    public async Task Create_NameWithoutLettersOrBlankTitle_IsABadRequest(string name, string title)
    {
        var commands = Substitute.For<IMenuCommands>();

        var result = await MenuEndpoints.Create(MenuRequest(name, title), new DefaultHttpContext(), commands);

        Assert.IsType<BadRequest>(result);
        await commands.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Theory]
    [MemberData(nameof(MenuConflicts))]
    public async Task Update_MapsEveryOutcomeOfTheCommand(MenuSaveResult outcome, int status, string? reason)
    {
        var commands = Substitute.For<IMenuCommands>();
        commands.Update(default, default!, default).ReturnsForAnyArgs(outcome);

        var result = await MenuEndpoints.Update(MenuId, MenuRequest(), new DefaultHttpContext(), commands);

        AssertOutcome(result, status, reason);
        await commands.Received(1).Update(MenuId, new IMenuCommands.MenuDraft("footer-links", "Footer"), Arg.Any<CancellationToken>());
    }

    public static TheoryData<MenuSaveResult, int, string?> MenuConflicts => new()
    {
        { new MenuSaveResult.Saved(new VersionedKey<Guid>(MenuId, 2)), 200, null },
        { new MenuSaveResult.NotFound(), 404, null },
        { new MenuSaveResult.NameTaken(), 409, "name-taken" },
        { new MenuSaveResult.BuiltIn(), 409, "built-in" },
        { new MenuSaveResult.VersionConflict(), 409, "version-conflict" }
    };

    [Fact]
    public async Task Update_InvalidRequest_IsABadRequest()
    {
        Assert.IsType<BadRequest>(
            await MenuEndpoints.Update(MenuId, MenuRequest("", "x"), new DefaultHttpContext(), Substitute.For<IMenuCommands>()));
    }

    [Theory]
    [InlineData(MenuRemovalResult.Removed, 204, null)]
    [InlineData(MenuRemovalResult.NotFound, 404, null)]
    [InlineData(MenuRemovalResult.BuiltIn, 409, "built-in")]
    public async Task Delete_MapsEveryOutcomeOfTheCommand(MenuRemovalResult outcome, int status, string? reason)
    {
        var commands = Substitute.For<IMenuCommands>();
        commands.Delete(MenuId, Arg.Any<CancellationToken>()).Returns(outcome);

        AssertOutcome(await MenuEndpoints.Delete(MenuId, new DefaultHttpContext(), commands), status, reason);
    }

    [Theory]
    [MemberData(nameof(RestoreOutcomes))]
    public async Task Restore_MapsEveryOutcomeOfTheCommand(MenuSaveResult outcome, int status, string? reason)
    {
        var commands = Substitute.For<IMenuCommands>();
        commands.Restore(MenuId, Arg.Any<CancellationToken>()).Returns(outcome);

        AssertOutcome(await MenuEndpoints.Restore(MenuId, new DefaultHttpContext(), commands), status, reason);
    }

    public static TheoryData<MenuSaveResult, int, string?> RestoreOutcomes => new()
    {
        { new MenuSaveResult.Saved(new VersionedKey<Guid>(MenuId, 1)), 204, null },
        { new MenuSaveResult.NotFound(), 404, null },
        { new MenuSaveResult.NameTaken(), 409, "name-taken" }
    };

    [Theory]
    [InlineData(HardDeleteResult.Deleted, 204)]
    [InlineData(HardDeleteResult.NotFound, 404)]
    [InlineData(HardDeleteResult.Rejected, 409)]
    public async Task HardDelete_MapsEveryOutcomeOfTheCommand(HardDeleteResult outcome, int status)
    {
        var commands = Substitute.For<IMenuCommands>();
        commands.HardDelete(MenuId, Arg.Any<CancellationToken>()).Returns(outcome);

        AssertOutcome(await MenuEndpoints.HardDelete(MenuId, new DefaultHttpContext(), commands), status, null);
    }

    [Fact]
    public async Task CreateItem_StoresTheTrimmedFieldsAndTheDistinctRoles()
    {
        var commands = Substitute.For<IMenuItemCommands>();
        commands.Create(default, default!, default).ReturnsForAnyArgs(new MenuItemSaveResult.Saved(new VersionedKey<Guid>(ItemId, 1)));

        var result = await MenuEndpoints.CreateItem(
            MenuId,
            ItemRequest(parent: ItemId, roles: [RoleId, RoleId]),
            new DefaultHttpContext(),
            commands);

        Assert.Equal(new NewsSavedResponse(ItemId, 1), Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await commands.Received(1).Create(
            MenuId,
            Arg.Is<IMenuItemCommands.MenuItemDraft>(draft =>
                draft.Title == "Docs"
                && draft.Url == "#/docs"
                && draft.Target == MenuTarget.NewTab
                && draft.Kind == MenuItemKind.Link
                && draft.Position == 10
                && draft.ParentId == Some(ItemId)
                && draft.RoleIds.SequenceEqual(new[] { RoleId })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateItem_WithoutRoles_StoresNone()
    {
        var commands = Substitute.For<IMenuItemCommands>();
        commands.Create(default, default!, default).ReturnsForAnyArgs(new MenuItemSaveResult.Saved(new VersionedKey<Guid>(ItemId, 1)));

        await MenuEndpoints.CreateItem(MenuId, ItemRequest(), new DefaultHttpContext(), commands);

        await commands.Received(1).Create(
            MenuId,
            Arg.Is<IMenuItemCommands.MenuItemDraft>(draft => draft.RoleIds.Count == 0 && draft.ParentId == None),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("   ", "#/docs")]
    [InlineData("Docs", "")]
    [InlineData("Docs", "javascript:alert(1)")]
    [InlineData("Docs", "data:text/html,<script>alert(1)</script>")]
    [InlineData("Docs", "# /docs")]
    public async Task CreateItem_BlankTitleOrUnsafeUrl_IsABadRequest(string title, string url)
    {
        var commands = Substitute.For<IMenuItemCommands>();

        var result = await MenuEndpoints.CreateItem(MenuId, ItemRequest(title, url), new DefaultHttpContext(), commands);

        Assert.IsType<BadRequest>(result);
        await commands.DidNotReceiveWithAnyArgs().Create(default, default!, default);
    }

    [Theory]
    [MemberData(nameof(ItemOutcomes))]
    public async Task UpdateItem_MapsEveryOutcomeOfTheCommand(MenuItemSaveResult outcome, int status, string? reason)
    {
        var commands = Substitute.For<IMenuItemCommands>();
        commands.Update(default, default!, default).ReturnsForAnyArgs(outcome);

        var result = await MenuEndpoints.UpdateItem(ItemId, ItemRequest(), new DefaultHttpContext(), commands);

        AssertOutcome(result, status, reason);
        await commands.Received(1).Update(ItemId, Arg.Any<IMenuItemCommands.MenuItemDraft>(), Arg.Any<CancellationToken>());
    }

    public static TheoryData<MenuItemSaveResult, int, string?> ItemOutcomes => new()
    {
        { new MenuItemSaveResult.Saved(new VersionedKey<Guid>(ItemId, 2)), 200, null },
        { new MenuItemSaveResult.NotFound(), 404, null },
        { new MenuItemSaveResult.UnknownMenu(), 409, "unknown-menu" },
        { new MenuItemSaveResult.InvalidParent(), 409, "invalid-parent" },
        { new MenuItemSaveResult.KindNotAllowed(), 409, "kind-not-allowed" },
        { new MenuItemSaveResult.UnknownRole(), 409, "unknown-role" },
        { new MenuItemSaveResult.VersionConflict(), 409, "version-conflict" }
    };

    [Fact]
    public async Task UpdateItem_UnsafeUrl_IsABadRequest()
    {
        Assert.IsType<BadRequest>(await MenuEndpoints.UpdateItem(
            ItemId,
            ItemRequest(url: "javascript:alert(1)"),
            new DefaultHttpContext(),
            Substitute.For<IMenuItemCommands>()));
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task DeleteItem_AnswersNoContentOrNotFound(bool deleted, int status)
    {
        var commands = Substitute.For<IMenuItemCommands>();
        commands.Delete(ItemId, Arg.Any<CancellationToken>()).Returns(deleted);

        AssertOutcome(await MenuEndpoints.DeleteItem(ItemId, new DefaultHttpContext(), commands), status, null);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task RestoreItem_AnswersNoContentOrNotFound(bool restored, int status)
    {
        var commands = Substitute.For<IMenuItemCommands>();
        commands.Restore(ItemId, Arg.Any<CancellationToken>()).Returns(restored);

        AssertOutcome(await MenuEndpoints.RestoreItem(ItemId, new DefaultHttpContext(), commands), status, null);
    }

    [Theory]
    [InlineData(HardDeleteResult.Deleted, 204)]
    [InlineData(HardDeleteResult.NotFound, 404)]
    [InlineData(HardDeleteResult.Rejected, 409)]
    public async Task HardDeleteItem_MapsEveryOutcomeOfTheCommand(HardDeleteResult outcome, int status)
    {
        var commands = Substitute.For<IMenuItemCommands>();
        commands.HardDelete(ItemId, Arg.Any<CancellationToken>()).Returns(outcome);

        AssertOutcome(await MenuEndpoints.HardDeleteItem(ItemId, new DefaultHttpContext(), commands), status, null);
    }

    private static void AssertOutcome(IResult result, int status, string? reason)
    {
        switch (result)
        {
            case IStatusCodeHttpResult { StatusCode: { } code } when code == status:
                break;
            case JsonHttpResult<NewsSavedResponse> when status == 200:
                break;
            default:
                Assert.Fail($"Expected {status} but got {result.GetType().Name}");
                break;
        }

        if (reason is not null)
        {
            Assert.Equal(reason, Assert.IsType<Conflict<NewsConflictResponse>>(result).Value!.Reason);
        }
    }
}
