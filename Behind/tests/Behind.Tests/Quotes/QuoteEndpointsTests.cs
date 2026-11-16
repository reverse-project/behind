using Behind.Common.WebApi;
using Behind.Quotes.Database;
using Behind.Quotes.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.Quotes;

public class QuoteEndpointsTests
{
    private static readonly Guid QuoteId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static IQuoteQueries.QuoteListing Listing(string text = "Keep it simple", string author = "Ada") =>
        new(new VersionedKey<Guid>(QuoteId, 1), text, author);

    private static PagedResult<IQuoteQueries.QuoteListing> Page(params IQuoteQueries.QuoteListing[] listings) =>
        new(
            listings.Length,
            1,
            1,
            10,
            listings.Length > 0 ? Some<IEnumerable<IQuoteQueries.QuoteListing>>(listings) : None);

    private static IQuoteQueries Queries(params IQuoteQueries.QuoteListing[] listings)
    {
        var queries = Substitute.For<IQuoteQueries>();
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page(listings));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs(["A"]);

        return queries;
    }

    private static void AssertStatus(IResult result, int status) =>
        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);

    [Fact]
    public async Task List_ReturnsTheActiveQuotesAndIsNotCached()
    {
        var queries = Queries(Listing());
        var context = new DefaultHttpContext();

        var result = await QuoteEndpoints.List(context, queries, 2, 5);

        var item = Assert.Single(Assert.IsType<JsonHttpResult<QuotesResponse>>(result).Value!.Quotes.Items);
        Assert.Equal((QuoteId, "Keep it simple", "Ada"), (item.Id, item.Text, item.Author));
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        await queries.Received(1).List(new Flipper(2, 5), None, None, DeletionState.Active, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manage_ReturnsTheQuotesWithLetters()
    {
        var result = await QuoteEndpoints.Manage(
            new DefaultHttpContext(),
            Queries(Listing()),
            new FakeAuthorizationService());

        var body = Assert.IsType<JsonHttpResult<QuoteManageResponse>>(result).Value!;
        Assert.Equal(["A"], body.Letters);
        Assert.False(Assert.Single(body.Quotes.Items).IsDeleted);
    }

    [Fact]
    public async Task Manage_PassesTheSearchLetterAndPaging()
    {
        var queries = Queries();

        await QuoteEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService(), 2, 5, " foo ", "f");

        await queries.Received(1).List(
            new Flipper(2, 5),
            Some("foo"),
            Some("F"),
            DeletionState.Active,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manage_UnknownLetter_IsABadRequest()
    {
        var result = await QuoteEndpoints.Manage(
            new DefaultHttpContext(),
            Queries(),
            new FakeAuthorizationService(),
            letter: "ab");

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Manage_DeletedQuotes_NeedTheDeletedRecordsGrant()
    {
        var result = await QuoteEndpoints.Manage(
            new DefaultHttpContext(),
            Queries(),
            new FakeAuthorizationService(),
            deleted: true);

        AssertStatus(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Manage_DeletedQuotes_AreMarkedDeleted()
    {
        var result = await QuoteEndpoints.Manage(
            new DefaultHttpContext(),
            Queries(Listing()),
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant),
            deleted: true);

        Assert.True(Assert.Single(Assert.IsType<JsonHttpResult<QuoteManageResponse>>(result).Value!.Quotes.Items).IsDeleted);
    }

    [Fact]
    public async Task Create_TrimsTheDraftAndAnswersTheSavedVersion()
    {
        var commands = Substitute.For<IQuoteCommands>();
        commands.Create(default!, default).ReturnsForAnyArgs(new QuoteSaveResult.Saved(new VersionedKey<Guid>(QuoteId, 1)));

        var result = await QuoteEndpoints.Create(
            new QuoteEndpoints.SaveQuoteRequest("  Keep it simple ", " Ada "),
            new DefaultHttpContext(),
            commands);

        var body = Assert.IsType<JsonHttpResult<QuoteSavedResponse>>(result).Value!;
        Assert.Equal((QuoteId, 1L), (body.Id, body.Version));
        await commands.Received(1).Create(
            new IQuoteCommands.QuoteDraft("Keep it simple", "Ada"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "Ada")]
    [InlineData("Text", "   ")]
    [InlineData(null, "Ada")]
    [InlineData("Text", null)]
    public async Task Create_WithoutTextOrAuthor_IsABadRequest(string? text, string? author)
    {
        var commands = Substitute.For<IQuoteCommands>();

        var result = await QuoteEndpoints.Create(
            new QuoteEndpoints.SaveQuoteRequest(text!, author!),
            new DefaultHttpContext(),
            commands);

        Assert.IsType<BadRequest>(result);
        await commands.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Fact]
    public async Task Create_TooLongText_IsABadRequest()
    {
        var result = await QuoteEndpoints.Create(
            new QuoteEndpoints.SaveQuoteRequest(new string('a', 1_001), "Ada"),
            new DefaultHttpContext(),
            Substitute.For<IQuoteCommands>());

        Assert.IsType<BadRequest>(result);
    }

    public static TheoryData<QuoteSaveResult, int, string?> SaveOutcomes => new()
    {
        { new QuoteSaveResult.Saved(new VersionedKey<Guid>(QuoteId, 2)), 200, null },
        { new QuoteSaveResult.NotFound(), 404, null },
        { new QuoteSaveResult.VersionConflict(), 409, "version-conflict" }
    };

    [Theory]
    [MemberData(nameof(SaveOutcomes))]
    public async Task Update_MapsEveryOutcomeOfTheCommand(QuoteSaveResult outcome, int status, string? reason)
    {
        var commands = Substitute.For<IQuoteCommands>();
        commands.Update(QuoteId, Arg.Any<IQuoteCommands.QuoteDraft>(), Arg.Any<CancellationToken>()).Returns(outcome);

        var result = await QuoteEndpoints.Update(
            QuoteId,
            new QuoteEndpoints.SaveQuoteRequest("Text", "Author"),
            new DefaultHttpContext(),
            commands);

        if (status == 200)
        {
            Assert.IsType<JsonHttpResult<QuoteSavedResponse>>(result);
        }
        else
        {
            AssertStatus(result, status);
        }

        if (reason is not null)
        {
            Assert.Equal(reason, Assert.IsType<Conflict<QuoteConflictResponse>>(result).Value!.Reason);
        }
    }

    [Fact]
    public async Task Update_WithoutText_IsABadRequest()
    {
        var result = await QuoteEndpoints.Update(
            QuoteId,
            new QuoteEndpoints.SaveQuoteRequest(" ", "Author"),
            new DefaultHttpContext(),
            Substitute.For<IQuoteCommands>());

        Assert.IsType<BadRequest>(result);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task Delete_AndRestore_MapTheOutcomeOfTheCommand(bool found, int status)
    {
        var commands = Substitute.For<IQuoteCommands>();
        commands.Delete(QuoteId, Arg.Any<CancellationToken>()).Returns(found);
        commands.Restore(QuoteId, Arg.Any<CancellationToken>()).Returns(found);

        AssertStatus(await QuoteEndpoints.Delete(QuoteId, new DefaultHttpContext(), commands), status);
        AssertStatus(await QuoteEndpoints.Restore(QuoteId, new DefaultHttpContext(), commands), status);
    }

    [Theory]
    [InlineData(HardDeleteResult.Deleted, 204)]
    [InlineData(HardDeleteResult.NotFound, 404)]
    [InlineData(HardDeleteResult.Rejected, 409)]
    public async Task HardDelete_MapsEveryOutcomeOfTheCommand(HardDeleteResult outcome, int status)
    {
        var commands = Substitute.For<IQuoteCommands>();
        commands.HardDelete(QuoteId, Arg.Any<CancellationToken>()).Returns(outcome);

        AssertStatus(await QuoteEndpoints.HardDelete(QuoteId, new DefaultHttpContext(), commands), status);
    }
}
