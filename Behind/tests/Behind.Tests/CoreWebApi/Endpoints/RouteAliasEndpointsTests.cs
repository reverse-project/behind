using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class RouteAliasEndpointsTests
{
    private static readonly Guid AliasId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static IRouteAliasQueries.RouteAliasListing Listing(
        string alias = "manuals",
        string target = "#/downloads/manuals") =>
        new(new VersionedKey<Guid>(AliasId, 1), alias, target);

    private static PagedResult<IRouteAliasQueries.RouteAliasListing> Page(
        params IRouteAliasQueries.RouteAliasListing[] listings) =>
        new(
            listings.Length,
            1,
            1,
            10,
            listings.Length > 0 ? Some<IEnumerable<IRouteAliasQueries.RouteAliasListing>>(listings) : None);

    private static RouteAliasEndpoints.SaveRouteAliasRequest Request(
        string alias = "Product Manuals",
        string target = " #/downloads/manuals ") =>
        new(alias, target);

    [Fact]
    public async Task Resolve_KnownAlias_ReturnsTheTargetAndIsNotCached()
    {
        var queries = Substitute.For<IRouteAliasQueries>();
        queries.FindTarget("manuals", Arg.Any<CancellationToken>()).Returns(Some("#/downloads/manuals"));
        var httpContext = new DefaultHttpContext();

        var result = await RouteAliasEndpoints.Resolve(" Manuals ", httpContext, queries);

        Assert.Equal(
            new RouteAliasTargetResponse("manuals", "#/downloads/manuals"),
            Assert.IsType<JsonHttpResult<RouteAliasTargetResponse>>(result).Value);
        Assert.Equal(ManagedListExtensions.NoStore, httpContext.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Resolve_UnknownAlias_IsNotFound()
    {
        var queries = Substitute.For<IRouteAliasQueries>();
        queries.FindTarget(default!, default).ReturnsForAnyArgs(None);

        Assert.IsType<NotFound>(await RouteAliasEndpoints.Resolve("missing", new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task Manage_ReturnsTheRouteAliasesWithLetters()
    {
        var queries = Substitute.For<IRouteAliasQueries>();
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page(Listing()));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs(["M"]);

        var result = await RouteAliasEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService());

        var body = Assert.IsType<JsonHttpResult<RouteAliasManageResponse>>(result).Value!;
        Assert.Equal(["M"], body.Letters);
        var item = Assert.Single(body.Aliases.Items);
        Assert.Equal((AliasId, "manuals", "#/downloads/manuals", false), (item.Id, item.Alias, item.Target, item.IsDeleted));
    }

    [Fact]
    public async Task Manage_PassesTheSearchLetterAndPaging()
    {
        var queries = Substitute.For<IRouteAliasQueries>();
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page());
        queries.GetLetters(default, default, default).ReturnsForAnyArgs([]);

        await RouteAliasEndpoints.Manage(new DefaultHttpContext(), queries, new FakeAuthorizationService(), 2, 5, " foo ", "f");

        await queries.Received(1).List(new Flipper(2, 5), Some("foo"), Some("F"), DeletionState.Active, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manage_DeletedRouteAliases_NeedTheDeletedRecordsGrant()
    {
        var result = await RouteAliasEndpoints.Manage(
            new DefaultHttpContext(),
            Substitute.For<IRouteAliasQueries>(),
            new FakeAuthorizationService(),
            deleted: true);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Manage_DeletedRouteAliases_AreMarkedDeleted()
    {
        var queries = Substitute.For<IRouteAliasQueries>();
        queries.List(default!, default, default, default, default).ReturnsForAnyArgs(Page(Listing()));
        queries.GetLetters(default, default, default).ReturnsForAnyArgs([]);

        var result = await RouteAliasEndpoints.Manage(
            new DefaultHttpContext(),
            queries,
            new FakeAuthorizationService(ManagedListExtensions.DeletedRecordsGrant),
            deleted: true);

        Assert.True(Assert.Single(Assert.IsType<JsonHttpResult<RouteAliasManageResponse>>(result).Value!.Aliases.Items).IsDeleted);
    }

    [Fact]
    public async Task Create_StoresTheSlugOfTheAliasAndTheTrimmedTarget()
    {
        var commands = Substitute.For<IRouteAliasCommands>();
        commands.Create(default!, default).ReturnsForAnyArgs(new RouteAliasSaveResult.Saved(new VersionedKey<Guid>(AliasId, 1)));

        var result = await RouteAliasEndpoints.Create(Request(), new DefaultHttpContext(), commands);

        Assert.Equal(new NewsSavedResponse(AliasId, 1), Assert.IsType<JsonHttpResult<NewsSavedResponse>>(result).Value);
        await commands.Received(1).Create(
            new IRouteAliasCommands.RouteAliasDraft("product-manuals", "#/downloads/manuals"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "#/downloads")]
    [InlineData("!!!", "#/downloads")]
    [InlineData("manuals", "")]
    [InlineData("manuals", "downloads")]
    [InlineData("manuals", "https://example.com")]
    [InlineData("manuals", "javascript:alert(1)")]
    [InlineData("manuals", "#/down loads")]
    public async Task Create_InvalidAliasOrTarget_IsABadRequest(string alias, string target)
    {
        var commands = Substitute.For<IRouteAliasCommands>();

        var result = await RouteAliasEndpoints.Create(Request(alias, target), new DefaultHttpContext(), commands);

        Assert.IsType<BadRequest>(result);
        await commands.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Theory]
    [MemberData(nameof(SaveOutcomes))]
    public async Task Update_MapsEveryOutcomeOfTheCommand(RouteAliasSaveResult outcome, int status, string? reason)
    {
        var commands = Substitute.For<IRouteAliasCommands>();
        commands.Update(default, default!, default).ReturnsForAnyArgs(outcome);

        var result = await RouteAliasEndpoints.Update(AliasId, Request(), new DefaultHttpContext(), commands);

        AssertOutcome(result, status, reason);
        await commands.Received(1).Update(
            AliasId,
            new IRouteAliasCommands.RouteAliasDraft("product-manuals", "#/downloads/manuals"),
            Arg.Any<CancellationToken>());
    }

    public static TheoryData<RouteAliasSaveResult, int, string?> SaveOutcomes => new()
    {
        { new RouteAliasSaveResult.Saved(new VersionedKey<Guid>(AliasId, 2)), 200, null },
        { new RouteAliasSaveResult.NotFound(), 404, null },
        { new RouteAliasSaveResult.AliasTaken(), 409, "alias-taken" },
        { new RouteAliasSaveResult.VersionConflict(), 409, "version-conflict" }
    };

    [Fact]
    public async Task Update_InvalidRequest_IsABadRequest()
    {
        Assert.IsType<BadRequest>(await RouteAliasEndpoints.Update(
            AliasId,
            Request("", "#/x"),
            new DefaultHttpContext(),
            Substitute.For<IRouteAliasCommands>()));
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task Delete_MapsTheOutcomeOfTheCommand(bool removed, int status)
    {
        var commands = Substitute.For<IRouteAliasCommands>();
        commands.Delete(AliasId, Arg.Any<CancellationToken>()).Returns(removed);

        AssertOutcome(await RouteAliasEndpoints.Delete(AliasId, new DefaultHttpContext(), commands), status, null);
    }

    [Theory]
    [MemberData(nameof(RestoreOutcomes))]
    public async Task Restore_MapsEveryOutcomeOfTheCommand(RouteAliasSaveResult outcome, int status, string? reason)
    {
        var commands = Substitute.For<IRouteAliasCommands>();
        commands.Restore(AliasId, Arg.Any<CancellationToken>()).Returns(outcome);

        AssertOutcome(await RouteAliasEndpoints.Restore(AliasId, new DefaultHttpContext(), commands), status, reason);
    }

    public static TheoryData<RouteAliasSaveResult, int, string?> RestoreOutcomes => new()
    {
        { new RouteAliasSaveResult.Saved(new VersionedKey<Guid>(AliasId, 1)), 204, null },
        { new RouteAliasSaveResult.NotFound(), 404, null },
        { new RouteAliasSaveResult.AliasTaken(), 409, "alias-taken" }
    };

    [Theory]
    [InlineData(HardDeleteResult.Deleted, 204)]
    [InlineData(HardDeleteResult.NotFound, 404)]
    [InlineData(HardDeleteResult.Rejected, 409)]
    public async Task HardDelete_MapsEveryOutcomeOfTheCommand(HardDeleteResult outcome, int status)
    {
        var commands = Substitute.For<IRouteAliasCommands>();
        commands.HardDelete(AliasId, Arg.Any<CancellationToken>()).Returns(outcome);

        AssertOutcome(await RouteAliasEndpoints.HardDelete(AliasId, new DefaultHttpContext(), commands), status, null);
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
