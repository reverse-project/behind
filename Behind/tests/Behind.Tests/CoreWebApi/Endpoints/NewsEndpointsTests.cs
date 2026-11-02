using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class NewsEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedTimeProvider(Now);
    private static readonly Guid NewsId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ImageId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly VersionedKey<Guid> Saved = new(NewsId, 2);

    private static readonly INewsQueries.NewsCategoryInfo Category = new(CategoryId, "Releases", Some(ImageId));

    private static INewsQueries.NewsListing Listing(Option<INewsQueries.NewsCategoryInfo> category = default) =>
        new(
            new VersionedKey<Guid>(NewsId, 3),
            "Launch",
            "<p>short</p>",
            "Ada",
            Now,
            Some(Now.AddHours(1)),
            category,
            3,
            false,
            Now,
            None);

    private static INewsQueries.ManagedNewsListing Managed(
        Option<long> active = default,
        Option<INewsQueries.NewsLiveInfo> live = default,
        long latest = 1) =>
        new(NewsId, latest, active, "Launch", "Ada", Now, None, None, 0, live);

    private static PagedResult<T> Page<T>(params T[] items) =>
        new(items.Length, 1, 1, 10, items.Length > 0 ? Some<IEnumerable<T>>(items) : None);

    private static INewsQueries.NewsDetail Detail(
        string content = "<p>c</p>",
        bool highlighted = false,
        DateTimeOffset? start = null,
        Option<DateTimeOffset> end = default) =>
        new(Saved, "Launch", "<p>s</p>", content, "Ada", Now, None, Some(Category), highlighted, start ?? Now, end);

    private static SaveNewsRequestBuilder Request => new();

    private sealed record SaveNewsRequestBuilder(
        string Title = "Launch",
        string? Summary = "<p>short</p>",
        string? Content = "<p>long</p>",
        Guid? CategoryId = null,
        bool Highlighted = false,
        DateTimeOffset? StartAt = null,
        DateTimeOffset? EndAt = null,
        bool Publish = false)
    {
        public NewsEndpoints.SaveNewsRequest Build() => new(Title, Summary, Content, CategoryId, Highlighted, StartAt, EndAt, Publish);
    }

    private static INewsCategoryQueries CategoryQueries(params INewsCategoryQueries.NewsCategoryListing[] listings)
    {
        var queries = Substitute.For<INewsCategoryQueries>();
        queries.ListActive(Arg.Any<CancellationToken>()).Returns(listings);

        return queries;
    }

    private static INewsCommands CommandsReturning(NewsSaveResult result)
    {
        var commands = Substitute.For<INewsCommands>();
        commands.Create(default, default!, default, default).ReturnsForAnyArgs(result);
        commands.SaveVersion(default, default!, default, default).ReturnsForAnyArgs(result);
        commands.Activate(default!, default).ReturnsForAnyArgs(result);

        return commands;
    }

    private static Task<IResult> CreateAs(NewsEndpoints.SaveNewsRequest request, INewsCommands commands, bool valid = true) =>
        NewsEndpoints.Create(
            request,
            SessionTestHelper.SignedIn(SessionId),
            SessionTestHelper.SessionQueries(SessionId, UserId, valid),
            commands);

    [Fact]
    public async Task List_ReturnsTheLiveNewsAndCategoriesWithoutCaching()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.List(new Flipper(1, NewsEndpoints.DefaultPageSize), None, Now, Arg.Any<CancellationToken>())
            .Returns(Page(Listing(Some(Category))));
        var categories = CategoryQueries(new INewsCategoryQueries.NewsCategoryListing(new VersionedKey<Guid>(CategoryId, 1), "Releases", Some(ImageId)));
        var context = new DefaultHttpContext();

        var result = await NewsEndpoints.List(context, queries, categories, Clock);

        var body = Assert.IsType<JsonHttpResult<PublicNewsListResponse>>(result).Value!;
        var headerUrl = $"/sites/images/{ImageId}";
        var category = new NewsCategoryItem(CategoryId, "Releases", ImageId, headerUrl);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(
            [new NewsListItem(NewsId, 3, "Launch", "<p>short</p>", "Ada", Now, Now.AddHours(1), category, 3, false, Now, null)],
            body.News.Items);
        Assert.Equal([category], body.Categories);
    }

    [Fact]
    public async Task List_ItemWithoutCategoryOrUpdate_HasNullsInTheResponse()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.List(default!, default, default, default).ReturnsForAnyArgs(
            Page(Listing() with { UpdatedAt = None, CreatedAt = None }));

        var result = await NewsEndpoints.List(new DefaultHttpContext(), queries, CategoryQueries(), Clock);

        var item = Assert.Single(Assert.IsType<JsonHttpResult<PublicNewsListResponse>>(result).Value!.News.Items);
        Assert.Equal((null, null, null), (item.Category, item.UpdatedAt, item.CreatedAt));
    }

    [Fact]
    public async Task List_PassesThePagingTheCategoryFilterAndTheCurrentTime()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.List(default!, default, default, default).ReturnsForAnyArgs(Page<INewsQueries.NewsListing>());

        await NewsEndpoints.List(new DefaultHttpContext(), queries, CategoryQueries(), Clock, 3, 1000, CategoryId);

        await queries.Received(1).List(
            new Flipper(3, NewsEndpoints.MaxPageSize),
            CategoryId,
            Now,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_ReportsWhichNewsIsHighlightedAndItsSchedule()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.List(default!, default, default, default).ReturnsForAnyArgs(Page(
            Listing() with { IsHighlighted = true, EndAt = Some(Now.AddDays(1)) },
            Listing()));

        var result = await NewsEndpoints.List(new DefaultHttpContext(), queries, CategoryQueries(), Clock);

        var items = Assert.IsType<JsonHttpResult<PublicNewsListResponse>>(result).Value!.News.Items;
        Assert.Equal([true, false], items.Select(item => item.IsHighlighted));
        Assert.Equal(Now.AddDays(1), items[0].EndAt);
    }

    [Fact]
    public async Task Manage_ReportsTheStatusOfEachNews()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.ListManaged(default!, default, default, default, default).ReturnsForAnyArgs(Page(
            Managed(),
            Managed(Some(1L), Some(new INewsQueries.NewsLiveInfo(None, None, false))),
            Managed(Some(1L), Some(new INewsQueries.NewsLiveInfo(Now.AddDays(1), None, false))),
            Managed(Some(1L), Some(new INewsQueries.NewsLiveInfo(Now.AddDays(-2), Some(Now.AddDays(-1)), false))),
            Managed(Some(1L), Some(new INewsQueries.NewsLiveInfo(Now.AddDays(-1), Some(Now.AddDays(1)), true)), latest: 3)));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs(["L"]);

        var result = await NewsEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService(), Clock);

        var body = Assert.IsType<JsonHttpResult<NewsManageResponse>>(result).Value!;
        Assert.Equal(["L"], body.Letters);
        Assert.Equal(["draft", "live", "scheduled", "expired", "live"], body.News.Items.Select(item => item.Status));
        Assert.Equal([false, false, false, false, true], body.News.Items.Select(item => item.IsHighlighted));
        Assert.Equal([false, false, false, false, true], body.News.Items.Select(item => item.HasNewerDraft));
        Assert.Equal((Now.AddDays(-1), Now.AddDays(1)), (body.News.Items[4].StartAt, body.News.Items[4].EndAt));
        Assert.Equal((null, null), (body.News.Items[0].StartAt, body.News.Items[0].EndAt));
        Assert.Equal((null, null), (body.News.Items[1].StartAt, body.News.Items[1].EndAt));
    }

    [Fact]
    public async Task Manage_PassesTheSearchLetterAndPaging()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.ListManaged(default!, default, default, default, default).ReturnsForAnyArgs(Page<INewsQueries.ManagedNewsListing>());
        queries.GetLetters(default, default, default).ReturnsForAnyArgs([]);

        await NewsEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService(), Clock, 2, 5, " launch ", "l");

        await queries.Received(1).ListManaged(new Flipper(2, 5), Some("launch"), Some("L"), DeletionState.Active, Arg.Any<CancellationToken>());
        await queries.Received(1).GetLetters(Some("launch"), DeletionState.Active, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manage_UnknownLetter_ReturnsBadRequest()
    {
        var result = await NewsEndpoints.Manage(
            new DefaultHttpContext(),
            Substitute.For<INewsQueries>(),
            new FakeAuthorizationService(),
            Clock,
            letter: "ab");

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Manage_DeletedNews_NeedTheDeletedRecordsGrant()
    {
        var queries = Substitute.For<INewsQueries>();

        var result = await NewsEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService(), Clock, deleted: true);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await queries.DidNotReceiveWithAnyArgs().ListManaged(default!, default, default, default, default);
    }

    [Fact]
    public async Task Manage_DeletedNews_AreListedForHoldersOfTheGrantAndMarkedDeleted()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.ListManaged(default!, default, default, default, default).ReturnsForAnyArgs(Page(Managed()));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs(["L"]);

        var result = await NewsEndpoints.Manage(
            new DefaultHttpContext(),
            queries,
            new FakeAuthorizationService(NewsEndpoints.DeletedRecordsGrant),
            Clock,
            deleted: true);

        Assert.True(Assert.Single(Assert.IsType<JsonHttpResult<NewsManageResponse>>(result).Value!.News.Items).IsDeleted);
        await queries.Received(1).ListManaged(Arg.Any<Flipper>(), None, None, DeletionState.Deleted, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_ReturnsTheLiveVersionAskingForTheCurrentTime()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.Find(NewsId, Now, Arg.Any<CancellationToken>()).Returns(Detail());
        var context = new DefaultHttpContext();

        var result = await NewsEndpoints.Get(NewsId, context, queries, Clock);

        var body = Assert.IsType<JsonHttpResult<NewsDetailResponse>>(result).Value!;
        Assert.Equal("no-cache", context.Response.Headers.CacheControl);
        Assert.Equal(
            (NewsId, 2L, "Launch", "<p>s</p>", "<p>c</p>", "<p>c</p>", "Ada", (DateTimeOffset?)Now),
            (body.Id, body.Version, body.Title, body.Summary, body.Content, body.DisplayContent, body.Author, body.CreatedAt));
        Assert.Equal(CategoryId, body.Category!.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Get_NewsWithoutContent_DisplaysTheSummaryButKeepsTheContentEmpty(string content)
    {
        var queries = Substitute.For<INewsQueries>();
        queries.Find(default, default, default).ReturnsForAnyArgs(Detail(content));

        var result = await NewsEndpoints.Get(NewsId, new DefaultHttpContext(), queries, Clock);

        var body = Assert.IsType<JsonHttpResult<NewsDetailResponse>>(result).Value!;
        Assert.Equal(("<p>s</p>", content), (body.DisplayContent, body.Content));
    }

    [Fact]
    public async Task Get_ReportsTheHighlightAndTheSchedule()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.Find(default, default, default).ReturnsForAnyArgs(Detail(highlighted: true, start: Now.AddDays(-1), end: Now.AddDays(2)));

        var result = await NewsEndpoints.Get(NewsId, new DefaultHttpContext(), queries, Clock);

        var body = Assert.IsType<JsonHttpResult<NewsDetailResponse>>(result).Value!;
        Assert.Equal((true, Now.AddDays(-1), (DateTimeOffset?)Now.AddDays(2)), (body.IsHighlighted, body.StartAt, body.EndAt));
    }

    [Fact]
    public async Task Get_UnscheduledNews_HasNoStartOrEnd()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.Find(default, default, default).ReturnsForAnyArgs(Detail() with { StartAt = None, EndAt = None });

        var result = await NewsEndpoints.Get(NewsId, new DefaultHttpContext(), queries, Clock);

        var body = Assert.IsType<JsonHttpResult<NewsDetailResponse>>(result).Value!;
        Assert.Equal((null, null), (body.StartAt, body.EndAt));
    }

    [Fact]
    public async Task Get_UnknownNews_ReturnsNotFound()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.Find(default, default, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await NewsEndpoints.Get(NewsId, new DefaultHttpContext(), queries, Clock));
    }

    [Fact]
    public async Task GetHistory_ReturnsTheVersionsWithoutCaching()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.FindHistory(NewsId, Arg.Any<CancellationToken>()).Returns(new INewsQueries.NewsHistory(
            NewsId,
            Some(1L),
            [
                new INewsQueries.NewsVersionInfo(2, "Draft", Now, None, true, Some(Now), false),
                new INewsQueries.NewsVersionInfo(1, "Live", Now, Some(Now.AddDays(1)), false, None, true)
            ]));
        var context = new DefaultHttpContext();

        var result = await NewsEndpoints.GetHistory(NewsId, context, queries);

        var body = Assert.IsType<JsonHttpResult<NewsHistoryResponse>>(result).Value!;
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal((NewsId, (long?)1), (body.Id, body.ActiveVersion));
        Assert.Equal(
            [new NewsVersionItem(2, "Draft", Now, null, true, Now, false), new NewsVersionItem(1, "Live", Now, Now.AddDays(1), false, null, true)],
            body.Versions);
    }

    [Fact]
    public async Task GetHistory_UnknownNews_ReturnsNotFound()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.FindHistory(default, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await NewsEndpoints.GetHistory(NewsId, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task GetVersion_ReturnsThatVersionWithoutCaching()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.FindVersion(Saved, Arg.Any<CancellationToken>()).Returns(Detail());
        var context = new DefaultHttpContext();

        var result = await NewsEndpoints.GetVersion(NewsId, 2, context, queries);

        Assert.Equal(2, Assert.IsType<JsonHttpResult<NewsDetailResponse>>(result).Value!.Version);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetVersion_UnknownVersion_ReturnsNotFound()
    {
        var queries = Substitute.For<INewsQueries>();
        queries.FindVersion(default!, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await NewsEndpoints.GetVersion(NewsId, 9, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task Create_StoresTheSanitizedDraftForTheSignedInAuthorWithThePublishFlag()
    {
        var commands = CommandsReturning(new NewsSaveResult.Saved(Saved));
        var request = (Request with
        {
            Title = "  Launch  ",
            Summary = "<p>s</p><script>x()</script>",
            Content = "<p onclick=\"x()\">c</p>",
            CategoryId = CategoryId,
            Publish = true
        }).Build();

        var result = await CreateAs(request, commands);

        Assert.Equal(new NewsSavedResponse(NewsId, 2), Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await commands.Received(1).Create(
            UserId,
            new INewsCommands.NewsDraft("Launch", "<p>s</p>", "<p>c</p>", CategoryId),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_PassesTheHighlightAndTheSchedule()
    {
        var commands = CommandsReturning(new NewsSaveResult.Saved(Saved));
        var start = Now.AddDays(1);

        await CreateAs((Request with { Highlighted = true, StartAt = start, EndAt = start.AddDays(2) }).Build(), commands);

        await commands.Received(1).Create(
            UserId,
            new INewsCommands.NewsDraft("Launch", "<p>short</p>", "<p>long</p>", None, true, start, start.AddDays(2)),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithoutAScheduleSendsNoStartOrEnd()
    {
        var commands = CommandsReturning(new NewsSaveResult.Saved(Saved));

        await CreateAs(Request.Build(), commands);

        await commands.Received(1).Create(
            UserId,
            Arg.Is<INewsCommands.NewsDraft>(draft => draft.StartAt == None && draft.EndAt == None),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_MissingContent_StoresEmptyText()
    {
        var commands = CommandsReturning(new NewsSaveResult.Saved(Saved));

        await CreateAs((Request with { Summary = null, Content = null }).Build(), commands);

        await commands.Received(1).Create(UserId, new INewsCommands.NewsDraft("Launch", "", "", None), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_BlankTitle_ReturnsBadRequest()
    {
        var commands = Substitute.For<INewsCommands>();

        Assert.IsType<BadRequest>(await CreateAs((Request with { Title = "   " }).Build(), commands));
        await commands.DidNotReceiveWithAnyArgs().Create(default, default!, default, default);
    }

    [Fact]
    public async Task Create_WithoutValidSession_ReturnsUnauthorized()
    {
        var commands = Substitute.For<INewsCommands>();

        Assert.IsType<UnauthorizedHttpResult>(await CreateAs(Request.Build(), commands, valid: false));
        await commands.DidNotReceiveWithAnyArgs().Create(default, default!, default, default);
    }

    [Theory]
    [InlineData("unknown-category")]
    [InlineData("invalid-schedule")]
    [InlineData("version-conflict")]
    public async Task Create_Rejections_ReturnConflictWithTheReason(string reason)
    {
        NewsSaveResult outcome = reason switch
        {
            "unknown-category" => new NewsSaveResult.UnknownCategory(),
            "invalid-schedule" => new NewsSaveResult.InvalidSchedule(),
            _ => new NewsSaveResult.VersionConflict()
        };

        var result = await CreateAs(Request.Build(), CommandsReturning(outcome));

        Assert.Equal(reason, Assert.IsType<Conflict<NewsConflictResponse>>(result).Value!.Reason);
    }

    [Fact]
    public async Task SaveVersion_StoresTheDraftWithThePublishFlag()
    {
        var commands = CommandsReturning(new NewsSaveResult.Saved(Saved));

        var result = await NewsEndpoints.SaveVersion(NewsId, (Request with { Publish = true }).Build(), new DefaultHttpContext(), commands);

        Assert.Equal(new NewsSavedResponse(NewsId, 2), Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await commands.Received(1).SaveVersion(
            NewsId,
            new INewsCommands.NewsDraft("Launch", "<p>short</p>", "<p>long</p>", None),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveVersion_UnknownNews_ReturnsNotFound()
    {
        var commands = CommandsReturning(new NewsSaveResult.NotFound());

        Assert.IsType<NotFound>(await NewsEndpoints.SaveVersion(NewsId, Request.Build(), new DefaultHttpContext(), commands));
    }

    [Fact]
    public async Task SaveVersion_BlankTitle_ReturnsBadRequest()
    {
        var result = await NewsEndpoints.SaveVersion(
            NewsId,
            (Request with { Title = " " }).Build(),
            new DefaultHttpContext(),
            Substitute.For<INewsCommands>());

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task SaveVersion_InvalidSchedule_ReturnsConflict()
    {
        var commands = CommandsReturning(new NewsSaveResult.InvalidSchedule());

        var result = await NewsEndpoints.SaveVersion(NewsId, Request.Build(), new DefaultHttpContext(), commands);

        Assert.Equal("invalid-schedule", Assert.IsType<Conflict<NewsConflictResponse>>(result).Value!.Reason);
    }

    [Fact]
    public async Task Activate_MakesTheVersionLive()
    {
        var commands = CommandsReturning(new NewsSaveResult.Saved(Saved));

        var result = await NewsEndpoints.Activate(NewsId, 2, new DefaultHttpContext(), commands);

        Assert.Equal(new NewsSavedResponse(NewsId, 2), Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await commands.Received(1).Activate(Saved, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activate_UnknownVersion_ReturnsNotFound()
    {
        var commands = CommandsReturning(new NewsSaveResult.NotFound());

        Assert.IsType<NotFound>(await NewsEndpoints.Activate(NewsId, 9, new DefaultHttpContext(), commands));
    }

    [Theory]
    [InlineData(true, typeof(NoContent))]
    [InlineData(false, typeof(NotFound))]
    public async Task Delete_ReturnsNoContentOrNotFound(bool deleted, Type expected)
    {
        var commands = Substitute.For<INewsCommands>();
        commands.Delete(NewsId, Arg.Any<CancellationToken>()).Returns(deleted);

        Assert.IsType(expected, await NewsEndpoints.Delete(NewsId, new DefaultHttpContext(), commands));
    }

    [Theory]
    [InlineData(true, typeof(NoContent))]
    [InlineData(false, typeof(NotFound))]
    public async Task Restore_ReturnsNoContentOrNotFound(bool restored, Type expected)
    {
        var commands = Substitute.For<INewsCommands>();
        commands.Restore(NewsId, Arg.Any<CancellationToken>()).Returns(restored);

        Assert.IsType(expected, await NewsEndpoints.Restore(NewsId, new DefaultHttpContext(), commands));
    }

    [Theory]
    [InlineData(HardDeleteResult.Deleted, typeof(NoContent))]
    [InlineData(HardDeleteResult.NotFound, typeof(NotFound))]
    [InlineData(HardDeleteResult.Rejected, typeof(Conflict))]
    public async Task HardDelete_MapsTheResult(HardDeleteResult outcome, Type expected)
    {
        var commands = Substitute.For<INewsCommands>();
        commands.HardDelete(NewsId, Arg.Any<CancellationToken>()).Returns(outcome);

        Assert.IsType(expected, await NewsEndpoints.HardDelete(NewsId, new DefaultHttpContext(), commands));
    }
}
