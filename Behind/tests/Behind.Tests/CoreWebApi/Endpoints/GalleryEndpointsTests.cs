using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class GalleryEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid GalleryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ThumbnailId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ImageId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ItemThumbnailId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly VersionedKey<Guid> Saved = new(GalleryId, 2);

    private static IGalleryQueries.GalleryListing Listing(Option<Guid> thumbnail = default) =>
        new(new VersionedKey<Guid>(GalleryId, 3), "Trip", "Summer", "Ada", Now, Some(Now.AddHours(1)), thumbnail, 4);

    private static IGalleryQueries.ManagedGalleryListing Managed(Option<long> active = default, long latest = 1) =>
        new(GalleryId, latest, active, "Trip", "Ada", Now, None, Some(ThumbnailId), 2);

    private static IGalleryQueries.GalleryDetail Detail(bool generated = false) =>
        new(
            Saved,
            "Trip",
            "Summer",
            "Ada",
            Now,
            None,
            Some(ThumbnailId),
            generated,
            [new IGalleryQueries.GalleryItemInfo(Guid.NewGuid(), "Beach", ImageId, ItemThumbnailId, 0)]);

    private static PagedResult<T> Page<T>(params T[] items) =>
        new(items.Length, 1, 1, 10, items.Length > 0 ? Some<IEnumerable<T>>(items) : None);

    private static GalleryEndpoints.SaveGalleryItemRequest ItemRequest(string? description = "Beach") =>
        new(ImageId, ItemThumbnailId, description);

    private static GalleryEndpoints.SaveGalleryRequest Request(
        string title = "Trip",
        string? description = "Summer",
        Guid? thumbnailImageId = null,
        IReadOnlyList<GalleryEndpoints.SaveGalleryItemRequest>? items = null,
        bool publish = false) =>
        new(title, description, thumbnailImageId, items ?? [ItemRequest()], publish);

    private static IGalleryCommands CommandsReturning(GallerySaveResult result)
    {
        var commands = Substitute.For<IGalleryCommands>();
        commands.Create(default, default!, default, default).ReturnsForAnyArgs(result);
        commands.SaveVersion(default, default!, default, default).ReturnsForAnyArgs(result);
        commands.Activate(default!, default).ReturnsForAnyArgs(result);

        return commands;
    }

    private static GalleryThumbnails Thumbnails()
    {
        var images = Substitute.For<IVfsQueries>();
        images.Open(default!, default, default).ReturnsForAnyArgs(None);

        return new GalleryThumbnails(images, Substitute.For<IVfsCommands>());
    }

    private static Task<IResult> CreateAs(
        GalleryEndpoints.SaveGalleryRequest request,
        IGalleryCommands commands,
        bool valid = true) =>
        GalleryEndpoints.Create(
            request,
            SessionTestHelper.SignedIn(SessionId),
            SessionTestHelper.SessionQueries(SessionId, UserId, valid),
            commands,
            Thumbnails());

    [Fact]
    public async Task List_ReturnsThePublishedGalleriesWithThumbnailUrlsWithoutCaching()
    {
        var queries = Substitute.For<IGalleryQueries>();
        queries.List(new Flipper(1, GalleryEndpoints.DefaultPageSize), None, Arg.Any<CancellationToken>())
            .Returns(Page(Listing(Some(ThumbnailId)), Listing()));
        var context = new DefaultHttpContext();

        var result = await GalleryEndpoints.List(context, queries);

        var body = Assert.IsType<JsonHttpResult<PublicGalleryListResponse>>(result).Value!;
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(
            new GalleryListItem(GalleryId, 3, "Trip", "Summer", "Ada", Now, Now.AddHours(1), $"/galleries/images/{ThumbnailId}", 4),
            body.Galleries.Items[0]);
        Assert.Null(body.Galleries.Items[1].ThumbnailUrl);
    }

    [Fact]
    public async Task List_PassesThePagingAndTheTrimmedSearch()
    {
        var queries = Substitute.For<IGalleryQueries>();
        queries.List(default!, default, default).ReturnsForAnyArgs(Page<IGalleryQueries.GalleryListing>());

        await GalleryEndpoints.List(new DefaultHttpContext(), queries, 3, 1000, "  trip ");

        await queries.Received(1).List(
            new Flipper(3, GalleryEndpoints.MaxPageSize),
            Some("trip"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manage_ReportsDraftOrPublishedAndNewerDrafts()
    {
        var queries = Substitute.For<IGalleryQueries>();
        queries.ListManaged(default!, default, default, default, default).ReturnsForAnyArgs(Page(
            Managed(),
            Managed(Some(1L)),
            Managed(Some(1L), latest: 3)));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs(["T"]);

        var result = await GalleryEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService());

        var body = Assert.IsType<JsonHttpResult<GalleryManageResponse>>(result).Value!;
        Assert.Equal(["T"], body.Letters);
        Assert.Equal(
            [("draft", false), ("published", false), ("published", true)],
            body.Galleries.Items.Select(item => (item.Status, item.HasNewerDraft)));
        Assert.All(body.Galleries.Items, item => Assert.False(item.IsDeleted));
        Assert.All(body.Galleries.Items, item => Assert.Equal($"/galleries/images/{ThumbnailId}", item.ThumbnailUrl));
    }

    [Fact]
    public async Task Manage_DeletedGalleries_NeedTheDeletedRecordsGrant()
    {
        var queries = Substitute.For<IGalleryQueries>();

        var result = await GalleryEndpoints.Manage(
            new DefaultHttpContext(),
            queries,
            new FakeAuthorizationService(),
            deleted: true);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
        await queries.DidNotReceiveWithAnyArgs().ListManaged(default!, default, default, default, default);
    }

    [Fact]
    public async Task Manage_DeletedGalleries_AreListedForHoldersOfTheGrantAndMarkedDeleted()
    {
        var queries = Substitute.For<IGalleryQueries>();
        queries.ListManaged(default!, default, default, default, default).ReturnsForAnyArgs(Page(Managed()));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs([]);

        var result = await GalleryEndpoints.Manage(
            new DefaultHttpContext(),
            queries,
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant),
            deleted: true);

        Assert.True(Assert.Single(Assert.IsType<JsonHttpResult<GalleryManageResponse>>(result).Value!.Galleries.Items).IsDeleted);
        await queries.Received(1).ListManaged(Arg.Any<Flipper>(), None, None, DeletionState.Deleted, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manage_UnknownLetter_ReturnsBadRequest()
    {
        var result = await GalleryEndpoints.Manage(
            new DefaultHttpContext(),
            Substitute.For<IGalleryQueries>(),
            new FakeAuthorizationService(),
            letter: "12");

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Get_ReturnsTheLiveVersionWithTheImageUrls()
    {
        var queries = Substitute.For<IGalleryQueries>();
        queries.Find(GalleryId, Arg.Any<CancellationToken>()).Returns(Detail(generated: true));
        var context = new DefaultHttpContext();

        var result = await GalleryEndpoints.Get(GalleryId, context, queries);

        var body = Assert.IsType<JsonHttpResult<GalleryDetailResponse>>(result).Value!;
        Assert.Equal("no-cache", context.Response.Headers.CacheControl);
        Assert.Equal(
            (GalleryId, 2L, "Trip", "Summer", "Ada", (Guid?)ThumbnailId, true),
            (body.Id, body.Version, body.Title, body.Description, body.Author, body.ThumbnailImageId, body.ThumbnailGenerated));
        Assert.Equal($"/galleries/images/{ThumbnailId}", body.ThumbnailUrl);
        var item = Assert.Single(body.Items);
        Assert.Equal(
            ("Beach", $"/galleries/images/{ImageId}", $"/galleries/images/{ItemThumbnailId}", 0),
            (item.Description, item.ImageUrl, item.ThumbnailUrl, item.Position));
    }

    [Fact]
    public async Task Get_UnknownGallery_ReturnsNotFound()
    {
        var queries = Substitute.For<IGalleryQueries>();
        queries.Find(default, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await GalleryEndpoints.Get(GalleryId, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task GetHistory_ReturnsTheVersionsWithoutCaching()
    {
        var queries = Substitute.For<IGalleryQueries>();
        queries.FindHistory(GalleryId, Arg.Any<CancellationToken>()).Returns(new IGalleryQueries.GalleryHistory(
            GalleryId,
            Some(1L),
            [
                new IGalleryQueries.GalleryVersionInfo(2, "Draft", 3, Now, false),
                new IGalleryQueries.GalleryVersionInfo(1, "Live", 2, None, true)
            ]));
        var context = new DefaultHttpContext();

        var result = await GalleryEndpoints.GetHistory(GalleryId, context, queries);

        var body = Assert.IsType<JsonHttpResult<GalleryHistoryResponse>>(result).Value!;
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal((GalleryId, (long?)1), (body.Id, body.ActiveVersion));
        Assert.Equal(
            [new GalleryVersionItem(2, "Draft", 3, Now, false), new GalleryVersionItem(1, "Live", 2, null, true)],
            body.Versions);
    }

    [Fact]
    public async Task GetHistory_UnknownGallery_ReturnsNotFound()
    {
        var queries = Substitute.For<IGalleryQueries>();
        queries.FindHistory(default, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await GalleryEndpoints.GetHistory(GalleryId, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task GetVersion_ReturnsThatVersionWithoutCaching()
    {
        var queries = Substitute.For<IGalleryQueries>();
        queries.FindVersion(Saved, Arg.Any<CancellationToken>()).Returns(Detail());
        var context = new DefaultHttpContext();

        var result = await GalleryEndpoints.GetVersion(GalleryId, 2, context, queries);

        Assert.Equal(2, Assert.IsType<JsonHttpResult<GalleryDetailResponse>>(result).Value!.Version);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetVersion_UnknownVersion_ReturnsNotFound()
    {
        var queries = Substitute.For<IGalleryQueries>();
        queries.FindVersion(default!, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await GalleryEndpoints.GetVersion(GalleryId, 9, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task Create_StoresTheTrimmedDraftForTheSignedInAuthorWithThePublishFlag()
    {
        var commands = CommandsReturning(new GallerySaveResult.Saved(Saved));
        var request = Request(
            title: "  Trip  ",
            description: " Summer ",
            thumbnailImageId: ThumbnailId,
            items: [ItemRequest("  Beach  ")],
            publish: true);

        var result = await CreateAs(request, commands);

        Assert.Equal(new NewsSavedResponse(GalleryId, 2), Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await commands.Received(1).Create(
            UserId,
            Arg.Is<IGalleryCommands.GalleryDraft>(draft =>
                draft.Title == "Trip"
                && draft.Description == "Summer"
                && draft.ThumbnailImageId == Some(ThumbnailId)
                && !draft.ThumbnailGenerated
                && draft.Items.SequenceEqual(new[] { new IGalleryCommands.GalleryItemDraft(ImageId, ItemThumbnailId, "Beach") })),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithoutAThumbnail_MarksItAsGenerated()
    {
        var commands = CommandsReturning(new GallerySaveResult.Saved(Saved));

        await CreateAs(Request(description: null, items: []), commands);

        await commands.Received(1).Create(
            UserId,
            Arg.Is<IGalleryCommands.GalleryDraft>(draft =>
                draft.Description == string.Empty && draft.ThumbnailGenerated && draft.Items.Count == 0),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_MissingItems_StoresAnEmptyGallery()
    {
        var commands = CommandsReturning(new GallerySaveResult.Saved(Saved));

        await CreateAs(new GalleryEndpoints.SaveGalleryRequest("Trip", null, null, null), commands);

        await commands.Received(1).Create(
            UserId,
            Arg.Is<IGalleryCommands.GalleryDraft>(draft => draft.Items.Count == 0),
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_InvalidRequest_ReturnsBadRequestWithoutStoring()
    {
        var commands = CommandsReturning(new GallerySaveResult.Saved(Saved));
        var requests = new[]
        {
            Request(title: "   "),
            Request(items: [new GalleryEndpoints.SaveGalleryItemRequest(Guid.Empty, ItemThumbnailId, "x")]),
            Request(items: [new GalleryEndpoints.SaveGalleryItemRequest(ImageId, Guid.Empty, "x")]),
            Request(items: [ItemRequest(new string('x', GalleryItem.MaxDescriptionLength + 1))]),
            Request(items: Enumerable.Repeat(ItemRequest(), Gallery.MaxItems + 1).ToList())
        };

        foreach (var request in requests)
        {
            Assert.IsType<BadRequest>(await CreateAs(request, commands));
        }

        await commands.DidNotReceiveWithAnyArgs().Create(default, default!, default, default);
    }

    [Fact]
    public async Task Create_WithoutAValidSession_ReturnsUnauthorized()
    {
        var commands = CommandsReturning(new GallerySaveResult.Saved(Saved));

        Assert.IsType<UnauthorizedHttpResult>(await CreateAs(Request(), commands, valid: false));
        await commands.DidNotReceiveWithAnyArgs().Create(default, default!, default, default);
    }

    [Fact]
    public async Task Create_MapsTheSaveResultsToStatusCodes()
    {
        var results = new (GallerySaveResult Result, string Reason)[]
        {
            (new GallerySaveResult.UnknownImage(), "unknown-image"),
            (new GallerySaveResult.VersionConflict(), "version-conflict")
        };

        foreach (var (result, reason) in results)
        {
            var response = await CreateAs(Request(), CommandsReturning(result));

            var conflict = Assert.IsType<Conflict<NewsConflictResponse>>(response);
            Assert.Equal(reason, conflict.Value!.Reason);
        }
    }

    [Fact]
    public async Task SaveVersion_StoresTheDraftForTheGalleryWithThePublishFlag()
    {
        var commands = CommandsReturning(new GallerySaveResult.Saved(Saved));

        var result = await GalleryEndpoints.SaveVersion(
            GalleryId,
            Request(publish: true),
            new DefaultHttpContext(),
            commands,
            Thumbnails());

        Assert.Equal(new NewsSavedResponse(GalleryId, 2), Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await commands.Received(1).SaveVersion(
            GalleryId,
            Arg.Is<IGalleryCommands.GalleryDraft>(draft => draft.Title == "Trip"),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveVersion_InvalidRequestOrUnknownGallery_IsRejected()
    {
        Assert.IsType<BadRequest>(await GalleryEndpoints.SaveVersion(
            GalleryId,
            Request(title: " "),
            new DefaultHttpContext(),
            CommandsReturning(new GallerySaveResult.Saved(Saved)),
            Thumbnails()));

        Assert.IsType<NotFound>(await GalleryEndpoints.SaveVersion(
            GalleryId,
            Request(),
            new DefaultHttpContext(),
            CommandsReturning(new GallerySaveResult.NotFound()),
            Thumbnails()));
    }

    [Fact]
    public async Task Activate_MakesTheVersionLive()
    {
        var commands = CommandsReturning(new GallerySaveResult.Saved(Saved));

        var result = await GalleryEndpoints.Activate(GalleryId, 2, new DefaultHttpContext(), commands);

        Assert.Equal(new NewsSavedResponse(GalleryId, 2), Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await commands.Received(1).Activate(Saved, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activate_UnknownVersion_ReturnsNotFound()
    {
        var unknown = await GalleryEndpoints.Activate(
            GalleryId,
            9,
            new DefaultHttpContext(),
            CommandsReturning(new GallerySaveResult.NotFound()));

        Assert.IsType<NotFound>(unknown);
    }

    [Fact]
    public async Task Delete_Restore_AndHardDelete_ReportTheOutcome()
    {
        var commands = Substitute.For<IGalleryCommands>();
        commands.Delete(GalleryId, Arg.Any<CancellationToken>()).Returns(true);
        commands.Restore(GalleryId, Arg.Any<CancellationToken>()).Returns(true);
        commands.HardDelete(GalleryId, Arg.Any<CancellationToken>()).Returns(HardDeleteResult.Deleted);
        commands.Delete(Guid.Empty, Arg.Any<CancellationToken>()).Returns(false);
        commands.Restore(Guid.Empty, Arg.Any<CancellationToken>()).Returns(false);
        commands.HardDelete(Guid.Empty, Arg.Any<CancellationToken>()).Returns(HardDeleteResult.NotFound);
        var context = new DefaultHttpContext();

        Assert.IsType<NoContent>(await GalleryEndpoints.Delete(GalleryId, context, commands));
        Assert.IsType<NoContent>(await GalleryEndpoints.Restore(GalleryId, context, commands));
        Assert.IsType<NoContent>(await GalleryEndpoints.HardDelete(GalleryId, context, commands));
        Assert.IsType<NotFound>(await GalleryEndpoints.Delete(Guid.Empty, context, commands));
        Assert.IsType<NotFound>(await GalleryEndpoints.Restore(Guid.Empty, context, commands));
        Assert.IsType<NotFound>(await GalleryEndpoints.HardDelete(Guid.Empty, context, commands));
    }

    [Fact]
    public async Task HardDelete_ARejectedDeletion_ReturnsConflict()
    {
        var commands = Substitute.For<IGalleryCommands>();
        commands.HardDelete(GalleryId, Arg.Any<CancellationToken>()).Returns(HardDeleteResult.Rejected);

        var result = await GalleryEndpoints.HardDelete(GalleryId, new DefaultHttpContext(), commands);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }
}
