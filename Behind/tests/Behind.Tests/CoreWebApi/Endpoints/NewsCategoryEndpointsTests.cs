using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class NewsCategoryEndpointsTests
{
    private static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ImageId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static INewsCategoryQueries.NewsCategoryListing Listing(Option<Guid> image = default) =>
        new(new VersionedKey<Guid>(CategoryId, 1), "Releases", image);

    private static PagedResult<INewsCategoryQueries.NewsCategoryListing> Page(
        params INewsCategoryQueries.NewsCategoryListing[] listings) =>
        new(listings.Length, 1, 1, 10, listings.Length > 0 ? Some<IEnumerable<INewsCategoryQueries.NewsCategoryListing>>(listings) : None);

    private static INewsCategoryCommands CommandsReturning(NewsCategorySaveResult result)
    {
        var commands = Substitute.For<INewsCategoryCommands>();
        commands.Create(default!, default).ReturnsForAnyArgs(result);
        commands.Update(default, default!, default).ReturnsForAnyArgs(result);
        commands.Restore(default, default).ReturnsForAnyArgs(result);

        return commands;
    }

    [Fact]
    public async Task ListActive_ReturnsTheCategoriesWithTheirHeaderImageUrl()
    {
        var queries = Substitute.For<INewsCategoryQueries>();
        queries.ListActive(Arg.Any<CancellationToken>()).Returns([Listing(ImageId), Listing()]);
        var context = new DefaultHttpContext();

        var result = await NewsCategoryEndpoints.ListActive(context, queries);

        var items = Assert.IsType<JsonHttpResult<List<NewsCategoryItem>>>(result).Value!;
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(new NewsCategoryItem(CategoryId, "Releases", ImageId, $"/news/images/{ImageId}"), items[0]);
        Assert.Equal(new NewsCategoryItem(CategoryId, "Releases", null, null), items[1]);
    }

    [Fact]
    public async Task Manage_ReturnsTheCategoriesWithLetters()
    {
        var queries = Substitute.For<INewsCategoryQueries>();
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page(Listing()));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs(["R"]);

        var result = await NewsCategoryEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService());

        var body = Assert.IsType<JsonHttpResult<NewsCategoryManageResponse>>(result).Value!;
        Assert.Equal(["R"], body.Letters);
        Assert.Equal("Releases", Assert.Single(body.Categories.Items).Name);
    }

    [Fact]
    public async Task Manage_PassesTheSearchLetterAndPaging()
    {
        var queries = Substitute.For<INewsCategoryQueries>();
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page());
        queries.GetLetters(default, default, default).ReturnsForAnyArgs([]);

        await NewsCategoryEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService(), 2, 5, " rel ", "r");

        await queries.Received(1).List(new Flipper(2, 5), Some("rel"), Some("R"), DeletionState.Active, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manage_DeletedCategories_NeedTheDeletedRecordsGrant()
    {
        var result = await NewsCategoryEndpoints.Manage(
            new DefaultHttpContext(),
            Substitute.For<INewsCategoryQueries>(),
            new FakeAuthorizationService(),
            deleted: true);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Manage_DeletedCategories_AreMarkedDeleted()
    {
        var queries = Substitute.For<INewsCategoryQueries>();
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page(Listing()));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs([]);

        var result = await NewsCategoryEndpoints.Manage(
            new DefaultHttpContext(),
            queries,
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant),
            deleted: true);

        Assert.True(Assert.Single(Assert.IsType<JsonHttpResult<NewsCategoryManageResponse>>(result).Value!.Categories.Items).IsDeleted);
    }

    [Fact]
    public async Task Create_StoresTheTrimmedNameAndImage()
    {
        var commands = CommandsReturning(new NewsCategorySaveResult.Saved(new VersionedKey<Guid>(CategoryId, 1)));

        var result = await NewsCategoryEndpoints.Create(
            new NewsCategoryEndpoints.SaveNewsCategoryRequest("  Releases ", ImageId),
            new DefaultHttpContext(),
            commands);

        Assert.Equal(CategoryId, Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value!.Id);
        await commands.Received(1).Create(
            new INewsCategoryCommands.NewsCategoryDraft("Releases", ImageId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_BlankName_ReturnsBadRequest()
    {
        var commands = Substitute.For<INewsCategoryCommands>();

        var result = await NewsCategoryEndpoints.Create(
            new NewsCategoryEndpoints.SaveNewsCategoryRequest("  ", null),
            new DefaultHttpContext(),
            commands);

        Assert.IsType<BadRequest>(result);
        await commands.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Theory]
    [InlineData("name-taken")]
    [InlineData("unknown-image")]
    public async Task Create_Conflicts_ReportTheReason(string reason)
    {
        NewsCategorySaveResult outcome = reason == "name-taken"
            ? new NewsCategorySaveResult.NameTaken()
            : new NewsCategorySaveResult.UnknownImage();

        var result = await NewsCategoryEndpoints.Create(
            new NewsCategoryEndpoints.SaveNewsCategoryRequest("Releases", null),
            new DefaultHttpContext(),
            CommandsReturning(outcome));

        Assert.Equal(reason, Assert.IsType<Conflict<NewsConflictResponse>>(result).Value!.Reason);
    }

    [Fact]
    public async Task Update_StoresTheDraft()
    {
        var commands = CommandsReturning(new NewsCategorySaveResult.Saved(new VersionedKey<Guid>(CategoryId, 1)));

        var result = await NewsCategoryEndpoints.Update(
            CategoryId,
            new NewsCategoryEndpoints.SaveNewsCategoryRequest("Releases", null),
            new DefaultHttpContext(),
            commands);

        Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result);
        await commands.Received(1).Update(
            CategoryId,
            new INewsCategoryCommands.NewsCategoryDraft("Releases", None),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_UnknownCategory_ReturnsNotFound()
    {
        var result = await NewsCategoryEndpoints.Update(
            CategoryId,
            new NewsCategoryEndpoints.SaveNewsCategoryRequest("Releases", null),
            new DefaultHttpContext(),
            CommandsReturning(new NewsCategorySaveResult.NotFound()));

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Update_BlankName_ReturnsBadRequest()
    {
        var result = await NewsCategoryEndpoints.Update(
            CategoryId,
            new NewsCategoryEndpoints.SaveNewsCategoryRequest("", null),
            new DefaultHttpContext(),
            Substitute.For<INewsCategoryCommands>());

        Assert.IsType<BadRequest>(result);
    }

    [Theory]
    [InlineData(true, typeof(NoContent))]
    [InlineData(false, typeof(NotFound))]
    public async Task Delete_ReturnsNoContentOrNotFound(bool deleted, Type expected)
    {
        var commands = Substitute.For<INewsCategoryCommands>();
        commands.Delete(CategoryId, Arg.Any<CancellationToken>()).Returns(deleted);

        Assert.IsType(expected, await NewsCategoryEndpoints.Delete(CategoryId, new DefaultHttpContext(), commands));
    }

    [Fact]
    public async Task Restore_ReturnsNoContent()
    {
        var commands = CommandsReturning(new NewsCategorySaveResult.Saved(new VersionedKey<Guid>(CategoryId, 1)));

        Assert.IsType<NoContent>(await NewsCategoryEndpoints.Restore(CategoryId, new DefaultHttpContext(), commands));
    }

    [Fact]
    public async Task Restore_NameTaken_ReturnsConflict()
    {
        var commands = CommandsReturning(new NewsCategorySaveResult.NameTaken());

        var result = await NewsCategoryEndpoints.Restore(CategoryId, new DefaultHttpContext(), commands);

        Assert.Equal("name-taken", Assert.IsType<Conflict<NewsConflictResponse>>(result).Value!.Reason);
    }

    [Fact]
    public async Task Restore_NotDeleted_ReturnsNotFound()
    {
        var commands = CommandsReturning(new NewsCategorySaveResult.NotFound());

        Assert.IsType<NotFound>(await NewsCategoryEndpoints.Restore(CategoryId, new DefaultHttpContext(), commands));
    }

    [Theory]
    [InlineData(HardDeleteResult.Deleted, typeof(NoContent))]
    [InlineData(HardDeleteResult.NotFound, typeof(NotFound))]
    [InlineData(HardDeleteResult.Rejected, typeof(Conflict))]
    public async Task HardDelete_MapsTheResult(HardDeleteResult outcome, Type expected)
    {
        var commands = Substitute.For<INewsCategoryCommands>();
        commands.HardDelete(CategoryId, Arg.Any<CancellationToken>()).Returns(outcome);

        Assert.IsType(expected, await NewsCategoryEndpoints.HardDelete(CategoryId, new DefaultHttpContext(), commands));
    }
}
