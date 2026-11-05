using Behind.Core.Database;
using Behind.Core.Database.Queries;
using static Behind.Tests.Helpers.RouteAliasTestBedExtensions;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class RouteAliasQueriesTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<PagedResult<IRouteAliasQueries.RouteAliasListing>> ListAsync(
        Option<string> search = default,
        Option<string> letter = default,
        DeletionState state = DeletionState.Active,
        Flipper? flipper = null)
    {
        await using var ctx = _bed.Read();

        return await new RouteAliasQueries(ctx).List(flipper ?? new Flipper(1, 10), search, letter, state);
    }

    private static IReadOnlyList<string> Aliases(PagedResult<IRouteAliasQueries.RouteAliasListing> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Alias).ToList());

    [Fact]
    public async Task List_ReturnsTheLatestVersionOfEveryRouteAliasOrderedByAlias()
    {
        var manuals = await _bed.CreateRouteAliasAsync("manuals", "#/downloads/a");
        await _bed.WithRouteAliasCommandsAsync(commands => commands.Update(manuals, Draft("manuals", "#/downloads/b")));
        await _bed.CreateRouteAliasAsync("blog", "#/news");

        var page = await ListAsync();

        Assert.Equal(2, page.Total);
        Assert.Equal(["blog", "manuals"], Aliases(page));
        var listing = page.Results.GetOrElse([]).Last();
        Assert.Equal(new VersionedKey<Guid>(manuals, 2), listing.Id);
        Assert.Equal("#/downloads/b", listing.Target);
    }

    [Fact]
    public async Task List_SearchMatchesTheAliasAndTheTarget()
    {
        await _bed.CreateRouteAliasAsync("manuals", "#/downloads/Products");
        await _bed.CreateRouteAliasAsync("blog", "#/news");

        Assert.Equal(["manuals"], Aliases(await ListAsync(Some("MANU"))));
        Assert.Equal(["manuals"], Aliases(await ListAsync(Some("products"))));
        Assert.Equal(["blog"], Aliases(await ListAsync(Some("#/news"))));
        Assert.Equal(2, (await ListAsync(Some("  "))).Total);
    }

    [Fact]
    public async Task List_LetterFiltersByTheFirstLetterOfTheAlias()
    {
        await _bed.CreateRouteAliasAsync("manuals", "#/downloads");
        await _bed.CreateRouteAliasAsync("blog", "#/news");

        Assert.Equal(["blog"], Aliases(await ListAsync(letter: Some("B"))));
    }

    [Fact]
    public async Task List_PagesTheResults()
    {
        await _bed.CreateRouteAliasAsync("a", "#/one");
        await _bed.CreateRouteAliasAsync("b", "#/two");
        await _bed.CreateRouteAliasAsync("c", "#/three");

        var page = await ListAsync(flipper: new Flipper(2, 2));

        Assert.Equal(3, page.Total);
        Assert.Equal(2, page.PageCount);
        Assert.Equal(["c"], Aliases(page));
    }

    [Fact]
    public async Task List_DeletedState_ReturnsOnlyDeletedRouteAliases()
    {
        await _bed.CreateRouteAliasAsync("manuals", "#/downloads");
        var gone = await _bed.CreateRouteAliasAsync("blog", "#/news");
        await _bed.WithRouteAliasCommandsAsync(commands => commands.Delete(gone));

        Assert.Equal(["blog"], Aliases(await ListAsync(state: DeletionState.Deleted)));
        Assert.Equal(["manuals"], Aliases(await ListAsync()));
    }

    [Fact]
    public async Task GetLetters_ReturnsTheFirstLettersOfTheMatchingAliases()
    {
        await _bed.CreateRouteAliasAsync("manuals", "#/downloads");
        await _bed.CreateRouteAliasAsync("blog", "#/news");

        var letters = await _bed.WithRouteAliasQueriesAsync(queries => queries.GetLetters(None, DeletionState.Active));

        Assert.Equal(["B", "M"], letters);
    }

    [Fact]
    public async Task FindTarget_ReturnsTheTargetOfTheLatestVersion()
    {
        var key = await _bed.CreateRouteAliasAsync("manuals", "#/downloads/a");
        await _bed.WithRouteAliasCommandsAsync(commands => commands.Update(key, Draft("manuals", "#/downloads/b")));

        var target = await _bed.WithRouteAliasQueriesAsync(queries => queries.FindTarget("manuals"));

        Assert.Equal(Some("#/downloads/b"), target);
    }

    [Fact]
    public async Task FindTarget_UnknownOrDeletedAlias_IsNone()
    {
        var key = await _bed.CreateRouteAliasAsync("manuals");

        Assert.Equal(None, await _bed.WithRouteAliasQueriesAsync(queries => queries.FindTarget("missing")));

        await _bed.WithRouteAliasCommandsAsync(commands => commands.Delete(key));

        Assert.Equal(None, await _bed.WithRouteAliasQueriesAsync(queries => queries.FindTarget("manuals")));
    }
}
