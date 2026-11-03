using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class ArticleEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ArticleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ImageId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly VersionedKey<Guid> Saved = new(ArticleId, 2);

    private static readonly IArticleQueries.ArticleCategoryInfo Category = new(CategoryId, "Tutorials", Some(ImageId));

    private static IArticleQueries.ArticleListing Listing(Option<IArticleQueries.ArticleCategoryInfo> category = default) =>
        new(new VersionedKey<Guid>(ArticleId, 3), "Setup", "<p>short</p>", "Ada", Now, Some(Now.AddHours(1)), category);

    private static IArticleQueries.ManagedArticleListing Managed(Option<long> active = default, long latest = 1) =>
        new(ArticleId, latest, active, "Setup", "Ada", Now, None, None);

    private static IArticleQueries.ArticleDetail Detail(string content = "<p>c</p>") =>
        new(Saved, "Setup", "<p>s</p>", content, "Ada", Now, None, Some(Category));

    private static PagedResult<T> Page<T>(params T[] items) =>
        new(items.Length, 1, 1, 10, items.Length > 0 ? Some<IEnumerable<T>>(items) : None);

    private static ArticleEndpoints.SaveArticleRequest Request(
        string title = "Setup",
        string? summary = "<p>short</p>",
        string? content = "<p>long</p>",
        Guid? categoryId = null,
        bool publish = false) =>
        new(title, summary, content, categoryId, publish);

    private static IArticleCategoryQueries CategoryQueries(params IArticleCategoryQueries.ArticleCategoryListing[] listings)
    {
        var queries = Substitute.For<IArticleCategoryQueries>();
        queries.ListActive(Arg.Any<CancellationToken>()).Returns(listings);

        return queries;
    }

    private static IArticleCommands CommandsReturning(ArticleSaveResult result)
    {
        var commands = Substitute.For<IArticleCommands>();
        commands.Create(default, default!, default, default).ReturnsForAnyArgs(result);
        commands.SaveVersion(default, default!, default, default).ReturnsForAnyArgs(result);
        commands.Activate(default!, default).ReturnsForAnyArgs(result);

        return commands;
    }

    private static Task<IResult> CreateAs(
        ArticleEndpoints.SaveArticleRequest request,
        IArticleCommands commands,
        bool valid = true) =>
        ArticleEndpoints.Create(
            request,
            SessionTestHelper.SignedIn(SessionId),
            SessionTestHelper.SessionQueries(SessionId, UserId, valid),
            commands);

    [Fact]
    public async Task List_ReturnsThePublishedArticlesAndCategoriesWithoutCaching()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.List(new Flipper(1, ArticleEndpoints.DefaultPageSize), None, None, Arg.Any<CancellationToken>())
            .Returns(Page(Listing(Some(Category))));
        var categories = CategoryQueries(
            new IArticleCategoryQueries.ArticleCategoryListing(new VersionedKey<Guid>(CategoryId, 1), "Tutorials", Some(ImageId)));
        var context = new DefaultHttpContext();

        var result = await ArticleEndpoints.List(context, queries, categories);

        var body = Assert.IsType<JsonHttpResult<PublicArticleListResponse>>(result).Value!;
        var category = new NewsCategoryItem(CategoryId, "Tutorials", ImageId, $"/articles/images/{ImageId}");
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(
            [new ArticleListItem(ArticleId, 3, "Setup", "<p>short</p>", "Ada", Now, Now.AddHours(1), category)],
            body.Articles.Items);
        Assert.Equal([category], body.Categories);
    }

    [Fact]
    public async Task List_ItemWithoutCategoryOrUpdate_HasNullsInTheResponse()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.List(default!, default, default, default).ReturnsForAnyArgs(
            Page(Listing() with { UpdatedAt = None, CreatedAt = None }));

        var result = await ArticleEndpoints.List(new DefaultHttpContext(), queries, CategoryQueries());

        var item = Assert.Single(Assert.IsType<JsonHttpResult<PublicArticleListResponse>>(result).Value!.Articles.Items);
        Assert.Equal((null, null, null), (item.Category, item.UpdatedAt, item.CreatedAt));
    }

    [Fact]
    public async Task List_PassesThePagingTheCategoryFilterAndTheTrimmedSearch()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.List(default!, default, default, default).ReturnsForAnyArgs(Page<IArticleQueries.ArticleListing>());

        await ArticleEndpoints.List(new DefaultHttpContext(), queries, CategoryQueries(), 3, 1000, CategoryId, "  setup ");

        await queries.Received(1).List(
            new Flipper(3, ArticleEndpoints.MaxPageSize),
            CategoryId,
            Some("setup"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_BlankSearch_MeansNoSearch()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.List(default!, default, default, default).ReturnsForAnyArgs(Page<IArticleQueries.ArticleListing>());

        await ArticleEndpoints.List(new DefaultHttpContext(), queries, CategoryQueries(), search: "   ");

        await queries.Received(1).List(Arg.Any<Flipper>(), None, None, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manage_ReportsDraftOrPublishedAndNewerDrafts()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.ListManaged(default!, default, default, default, default).ReturnsForAnyArgs(Page(
            Managed(),
            Managed(Some(1L)),
            Managed(Some(1L), latest: 3)));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs(["S"]);

        var result = await ArticleEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService());

        var body = Assert.IsType<JsonHttpResult<ArticleManageResponse>>(result).Value!;
        Assert.Equal(["S"], body.Letters);
        Assert.Equal(
            [("draft", false), ("published", false), ("published", true)],
            body.Articles.Items.Select(item => (item.Status, item.HasNewerDraft)));
        Assert.All(body.Articles.Items, item => Assert.False(item.IsDeleted));
    }

    [Fact]
    public async Task Manage_PassesTheSearchLetterAndPaging()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.ListManaged(default!, default, default, default, default).ReturnsForAnyArgs(Page<IArticleQueries.ManagedArticleListing>());
        queries.GetLetters(default, default, default).ReturnsForAnyArgs([]);

        await ArticleEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService(), 2, 5, " setup ", "s");

        await queries.Received(1).ListManaged(new Flipper(2, 5), Some("setup"), Some("S"), DeletionState.Active, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manage_UnknownLetter_ReturnsBadRequest()
    {
        var result = await ArticleEndpoints.Manage(
            new DefaultHttpContext(),
            Substitute.For<IArticleQueries>(),
            new FakeAuthorizationService(),
            letter: "12");

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Manage_DeletedArticles_NeedTheDeletedRecordsGrant()
    {
        var queries = Substitute.For<IArticleQueries>();

        var result = await ArticleEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService(), deleted: true);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await queries.DidNotReceiveWithAnyArgs().ListManaged(default!, default, default, default, default);
    }

    [Fact]
    public async Task Manage_DeletedArticles_AreListedForHoldersOfTheGrantAndMarkedDeleted()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.ListManaged(default!, default, default, default, default).ReturnsForAnyArgs(Page(Managed()));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs([]);

        var result = await ArticleEndpoints.Manage(
            new DefaultHttpContext(),
            queries,
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant),
            deleted: true);

        Assert.True(Assert.Single(Assert.IsType<JsonHttpResult<ArticleManageResponse>>(result).Value!.Articles.Items).IsDeleted);
        await queries.Received(1).ListManaged(Arg.Any<Flipper>(), None, None, DeletionState.Deleted, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_ReturnsTheLiveVersionWithoutExtraFields()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.Find(ArticleId, Arg.Any<CancellationToken>()).Returns(Detail());
        var context = new DefaultHttpContext();

        var result = await ArticleEndpoints.Get(ArticleId, context, queries);

        var body = Assert.IsType<JsonHttpResult<ArticleDetailResponse>>(result).Value!;
        Assert.Equal("no-cache", context.Response.Headers.CacheControl);
        Assert.Equal(
            (ArticleId, 2L, "Setup", "<p>s</p>", "<p>c</p>", "<p>c</p>", "Ada", (DateTimeOffset?)Now),
            (body.Id, body.Version, body.Title, body.Summary, body.Content, body.DisplayContent, body.Author, body.CreatedAt));
        Assert.Equal(CategoryId, body.Category!.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Get_ArticleWithoutContent_DisplaysTheSummaryButKeepsTheContentEmpty(string content)
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.Find(default, default).ReturnsForAnyArgs(Detail(content));

        var result = await ArticleEndpoints.Get(ArticleId, new DefaultHttpContext(), queries);

        var body = Assert.IsType<JsonHttpResult<ArticleDetailResponse>>(result).Value!;
        Assert.Equal(("<p>s</p>", content), (body.DisplayContent, body.Content));
    }

    [Fact]
    public async Task Get_UnknownArticle_ReturnsNotFound()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.Find(default, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await ArticleEndpoints.Get(ArticleId, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task GetHistory_ReturnsTheVersionsWithoutCaching()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.FindHistory(ArticleId, Arg.Any<CancellationToken>()).Returns(new IArticleQueries.ArticleHistory(
            ArticleId,
            Some(1L),
            [
                new IArticleQueries.ArticleVersionInfo(2, "Draft", Now, false),
                new IArticleQueries.ArticleVersionInfo(1, "Live", None, true)
            ]));
        var context = new DefaultHttpContext();

        var result = await ArticleEndpoints.GetHistory(ArticleId, context, queries);

        var body = Assert.IsType<JsonHttpResult<ArticleHistoryResponse>>(result).Value!;
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal((ArticleId, (long?)1), (body.Id, body.ActiveVersion));
        Assert.Equal(
            [new ArticleVersionItem(2, "Draft", Now, false), new ArticleVersionItem(1, "Live", null, true)],
            body.Versions);
    }

    [Fact]
    public async Task GetHistory_UnknownArticle_ReturnsNotFound()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.FindHistory(default, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await ArticleEndpoints.GetHistory(ArticleId, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task GetVersion_ReturnsThatVersionWithoutCaching()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.FindVersion(Saved, Arg.Any<CancellationToken>()).Returns(Detail());
        var context = new DefaultHttpContext();

        var result = await ArticleEndpoints.GetVersion(ArticleId, 2, context, queries);

        Assert.Equal(2, Assert.IsType<JsonHttpResult<ArticleDetailResponse>>(result).Value!.Version);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetVersion_UnknownVersion_ReturnsNotFound()
    {
        var queries = Substitute.For<IArticleQueries>();
        queries.FindVersion(default!, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await ArticleEndpoints.GetVersion(ArticleId, 9, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task Create_StoresTheSanitizedDraftForTheSignedInAuthorWithThePublishFlag()
    {
        var commands = CommandsReturning(new ArticleSaveResult.Saved(Saved));
        var request = Request(
            title: "  Setup  ",
            summary: "<p>s</p><script>x()</script>",
            content: "<p onclick=\"x()\">c</p>",
            categoryId: CategoryId,
            publish: true);

        var result = await CreateAs(request, commands);

        Assert.Equal(new NewsSavedResponse(ArticleId, 2), Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await commands.Received(1).Create(
            UserId,
            new IArticleCommands.ArticleDraft("Setup", "<p>s</p>", "<p>c</p>", CategoryId),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_MissingContent_StoresEmptyText()
    {
        var commands = CommandsReturning(new ArticleSaveResult.Saved(Saved));

        await CreateAs(Request(summary: null, content: null), commands);

        await commands.Received(1).Create(
            UserId,
            new IArticleCommands.ArticleDraft("Setup", "", "", None),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_BlankTitle_ReturnsBadRequest()
    {
        var commands = Substitute.For<IArticleCommands>();

        Assert.IsType<BadRequest>(await CreateAs(Request(title: "   "), commands));
        await commands.DidNotReceiveWithAnyArgs().Create(default, default!, default, default);
    }

    [Fact]
    public async Task Create_WithoutValidSession_ReturnsUnauthorized()
    {
        var commands = Substitute.For<IArticleCommands>();

        Assert.IsType<UnauthorizedHttpResult>(await CreateAs(Request(), commands, valid: false));
        await commands.DidNotReceiveWithAnyArgs().Create(default, default!, default, default);
    }

    [Theory]
    [InlineData("unknown-category")]
    [InlineData("version-conflict")]
    public async Task Create_Rejections_ReturnConflictWithTheReason(string reason)
    {
        ArticleSaveResult outcome = reason == "unknown-category"
            ? new ArticleSaveResult.UnknownCategory()
            : new ArticleSaveResult.VersionConflict();

        var result = await CreateAs(Request(), CommandsReturning(outcome));

        Assert.Equal(reason, Assert.IsType<Conflict<NewsConflictResponse>>(result).Value!.Reason);
    }

    [Fact]
    public async Task SaveVersion_StoresTheDraftWithThePublishFlag()
    {
        var commands = CommandsReturning(new ArticleSaveResult.Saved(Saved));

        var result = await ArticleEndpoints.SaveVersion(
            ArticleId,
            Request(publish: true),
            new DefaultHttpContext(),
            commands);

        Assert.Equal(new NewsSavedResponse(ArticleId, 2), Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await commands.Received(1).SaveVersion(
            ArticleId,
            new IArticleCommands.ArticleDraft("Setup", "<p>short</p>", "<p>long</p>", None),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveVersion_UnknownArticle_ReturnsNotFound()
    {
        var commands = CommandsReturning(new ArticleSaveResult.NotFound());

        Assert.IsType<NotFound>(
            await ArticleEndpoints.SaveVersion(ArticleId, Request(), new DefaultHttpContext(), commands));
    }

    [Fact]
    public async Task SaveVersion_BlankTitle_ReturnsBadRequest()
    {
        var commands = Substitute.For<IArticleCommands>();

        Assert.IsType<BadRequest>(
            await ArticleEndpoints.SaveVersion(ArticleId, Request(title: " "), new DefaultHttpContext(), commands));
    }

    [Fact]
    public async Task Activate_ReturnsTheActivatedVersion()
    {
        var commands = CommandsReturning(new ArticleSaveResult.Saved(Saved));

        var result = await ArticleEndpoints.Activate(ArticleId, 2, new DefaultHttpContext(), commands);

        Assert.Equal(new NewsSavedResponse(ArticleId, 2), Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await commands.Received(1).Activate(Saved, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activate_UnknownVersion_ReturnsNotFound()
    {
        var commands = CommandsReturning(new ArticleSaveResult.NotFound());

        Assert.IsType<NotFound>(await ArticleEndpoints.Activate(ArticleId, 9, new DefaultHttpContext(), commands));
    }

    [Theory]
    [InlineData(true, typeof(NoContent))]
    [InlineData(false, typeof(NotFound))]
    public async Task Delete_ReturnsNoContentOrNotFound(bool deleted, Type expected)
    {
        var commands = Substitute.For<IArticleCommands>();
        commands.Delete(ArticleId, Arg.Any<CancellationToken>()).Returns(deleted);

        Assert.IsType(expected, await ArticleEndpoints.Delete(ArticleId, new DefaultHttpContext(), commands));
    }

    [Theory]
    [InlineData(true, typeof(NoContent))]
    [InlineData(false, typeof(NotFound))]
    public async Task Restore_ReturnsNoContentOrNotFound(bool restored, Type expected)
    {
        var commands = Substitute.For<IArticleCommands>();
        commands.Restore(ArticleId, Arg.Any<CancellationToken>()).Returns(restored);

        Assert.IsType(expected, await ArticleEndpoints.Restore(ArticleId, new DefaultHttpContext(), commands));
    }

    [Theory]
    [InlineData(HardDeleteResult.Deleted, typeof(NoContent))]
    [InlineData(HardDeleteResult.NotFound, typeof(NotFound))]
    [InlineData(HardDeleteResult.Rejected, typeof(Conflict))]
    public async Task HardDelete_MapsTheResult(HardDeleteResult outcome, Type expected)
    {
        var commands = Substitute.For<IArticleCommands>();
        commands.HardDelete(ArticleId, Arg.Any<CancellationToken>()).Returns(outcome);

        Assert.IsType(expected, await ArticleEndpoints.HardDelete(ArticleId, new DefaultHttpContext(), commands));
    }
}
