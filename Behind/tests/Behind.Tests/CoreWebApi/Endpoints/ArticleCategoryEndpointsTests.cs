using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class ArticleCategoryEndpointsTests
{
    private static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ImageId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly ArticleCategorySaveResult.Saved SavedResult = new(new VersionedKey<Guid>(CategoryId, 1));

    private static IArticleCategoryQueries.ArticleCategoryListing Listing(Option<Guid> image = default) =>
        new(new VersionedKey<Guid>(CategoryId, 1), "Tutorials", image);

    private static PagedResult<IArticleCategoryQueries.ArticleCategoryListing> Page(
        params IArticleCategoryQueries.ArticleCategoryListing[] listings) =>
        new(
            listings.Length,
            1,
            1,
            10,
            listings.Length > 0 ? Some<IEnumerable<IArticleCategoryQueries.ArticleCategoryListing>>(listings) : None);

    private static IArticleCategoryCommands CommandsReturning(ArticleCategorySaveResult result)
    {
        var commands = Substitute.For<IArticleCategoryCommands>();
        commands.Create(default!, default).ReturnsForAnyArgs(result);
        commands.Update(default, default!, default).ReturnsForAnyArgs(result);
        commands.Restore(default, default).ReturnsForAnyArgs(result);

        return commands;
    }

    private static ArticleCategoryEndpoints.SaveArticleCategoryRequest Request(string name = "Tutorials", Guid? image = null) =>
        new(name, image);

    [Fact]
    public async Task ListActive_ReturnsTheCategoriesWithTheirHeaderImageUrl()
    {
        var queries = Substitute.For<IArticleCategoryQueries>();
        queries.ListActive(Arg.Any<CancellationToken>()).Returns([Listing(ImageId), Listing()]);
        var context = new DefaultHttpContext();

        var result = await ArticleCategoryEndpoints.ListActive(context, queries);

        var items = Assert.IsType<JsonHttpResult<List<NewsCategoryItem>>>(result).Value!;
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(new NewsCategoryItem(CategoryId, "Tutorials", ImageId, $"/sites/images/{ImageId}"), items[0]);
        Assert.Equal(new NewsCategoryItem(CategoryId, "Tutorials", null, null), items[1]);
    }

    [Fact]
    public async Task Manage_ReturnsTheCategoriesWithLetters()
    {
        var queries = Substitute.For<IArticleCategoryQueries>();
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page(Listing()));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs(["T"]);

        var result = await ArticleCategoryEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService());

        var body = Assert.IsType<JsonHttpResult<ArticleCategoryManageResponse>>(result).Value!;
        Assert.Equal(["T"], body.Letters);
        Assert.Equal("Tutorials", Assert.Single(body.Categories.Items).Name);
    }

    [Fact]
    public async Task Manage_PassesTheSearchLetterAndPaging()
    {
        var queries = Substitute.For<IArticleCategoryQueries>();
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page());
        queries.GetLetters(default, default, default).ReturnsForAnyArgs([]);

        await ArticleCategoryEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService(), 2, 5, " tut ", "t");

        await queries.Received(1).List(new Flipper(2, 5), Some("tut"), Some("T"), DeletionState.Active, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manage_DeletedCategories_NeedTheDeletedRecordsGrant()
    {
        var result = await ArticleCategoryEndpoints.Manage(
            new DefaultHttpContext(),
            Substitute.For<IArticleCategoryQueries>(),
            new FakeAuthorizationService(),
            deleted: true);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Manage_DeletedCategories_AreMarkedDeleted()
    {
        var queries = Substitute.For<IArticleCategoryQueries>();
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page(Listing()));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs([]);

        var result = await ArticleCategoryEndpoints.Manage(
            new DefaultHttpContext(),
            queries,
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant),
            deleted: true);

        Assert.True(Assert.Single(Assert.IsType<JsonHttpResult<ArticleCategoryManageResponse>>(result).Value!.Categories.Items).IsDeleted);
    }

    [Fact]
    public async Task Create_StoresTheTrimmedNameAndImage()
    {
        var commands = CommandsReturning(SavedResult);

        var result = await ArticleCategoryEndpoints.Create(Request("  Tutorials ", ImageId), new DefaultHttpContext(), commands);

        Assert.Equal(CategoryId, Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value!.Id);
        await commands.Received(1).Create(
            new IArticleCategoryCommands.ArticleCategoryDraft("Tutorials", ImageId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_BlankName_ReturnsBadRequest()
    {
        var commands = Substitute.For<IArticleCategoryCommands>();

        var result = await ArticleCategoryEndpoints.Create(Request("  "), new DefaultHttpContext(), commands);

        Assert.IsType<BadRequest>(result);
        await commands.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Theory]
    [InlineData("name-taken")]
    [InlineData("unknown-image")]
    [InlineData("version-conflict")]
    public async Task Create_Conflicts_ReportTheReason(string reason)
    {
        ArticleCategorySaveResult outcome = reason switch
        {
            "name-taken" => new ArticleCategorySaveResult.NameTaken(),
            "unknown-image" => new ArticleCategorySaveResult.UnknownImage(),
            _ => new ArticleCategorySaveResult.VersionConflict()
        };

        var result = await ArticleCategoryEndpoints.Create(
            Request(),
            new DefaultHttpContext(),
            CommandsReturning(outcome));

        Assert.Equal(reason, Assert.IsType<Conflict<NewsConflictResponse>>(result).Value!.Reason);
    }

    [Fact]
    public async Task Update_StoresTheDraft()
    {
        var commands = CommandsReturning(SavedResult);

        var result = await ArticleCategoryEndpoints.Update(CategoryId, Request(), new DefaultHttpContext(), commands);

        Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result);
        await commands.Received(1).Update(
            CategoryId,
            new IArticleCategoryCommands.ArticleCategoryDraft("Tutorials", None),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_UnknownCategory_ReturnsNotFound()
    {
        var result = await ArticleCategoryEndpoints.Update(
            CategoryId,
            Request(),
            new DefaultHttpContext(),
            CommandsReturning(new ArticleCategorySaveResult.NotFound()));

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Update_BlankName_ReturnsBadRequest()
    {
        var result = await ArticleCategoryEndpoints.Update(
            CategoryId,
            Request(""),
            new DefaultHttpContext(),
            Substitute.For<IArticleCategoryCommands>());

        Assert.IsType<BadRequest>(result);
    }

    [Theory]
    [InlineData(true, typeof(NoContent))]
    [InlineData(false, typeof(NotFound))]
    public async Task Delete_ReturnsNoContentOrNotFound(bool deleted, Type expected)
    {
        var commands = Substitute.For<IArticleCategoryCommands>();
        commands.Delete(CategoryId, Arg.Any<CancellationToken>()).Returns(deleted);

        Assert.IsType(expected, await ArticleCategoryEndpoints.Delete(CategoryId, new DefaultHttpContext(), commands));
    }

    [Fact]
    public async Task Restore_ReturnsNoContent()
    {
        var commands = CommandsReturning(SavedResult);

        Assert.IsType<NoContent>(await ArticleCategoryEndpoints.Restore(CategoryId, new DefaultHttpContext(), commands));
    }

    [Fact]
    public async Task Restore_NameTaken_ReturnsConflict()
    {
        var commands = CommandsReturning(new ArticleCategorySaveResult.NameTaken());

        var result = await ArticleCategoryEndpoints.Restore(CategoryId, new DefaultHttpContext(), commands);

        Assert.Equal("name-taken", Assert.IsType<Conflict<NewsConflictResponse>>(result).Value!.Reason);
    }

    [Fact]
    public async Task Restore_NotDeleted_ReturnsNotFound()
    {
        var commands = CommandsReturning(new ArticleCategorySaveResult.NotFound());

        Assert.IsType<NotFound>(await ArticleCategoryEndpoints.Restore(CategoryId, new DefaultHttpContext(), commands));
    }

    [Theory]
    [InlineData(HardDeleteResult.Deleted, typeof(NoContent))]
    [InlineData(HardDeleteResult.NotFound, typeof(NotFound))]
    [InlineData(HardDeleteResult.Rejected, typeof(Conflict))]
    public async Task HardDelete_MapsTheResult(HardDeleteResult outcome, Type expected)
    {
        var commands = Substitute.For<IArticleCategoryCommands>();
        commands.HardDelete(CategoryId, Arg.Any<CancellationToken>()).Returns(outcome);

        Assert.IsType(expected, await ArticleCategoryEndpoints.HardDelete(CategoryId, new DefaultHttpContext(), commands));
    }
}
