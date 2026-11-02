using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class NewsCategoryQueriesTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<PagedResult<INewsCategoryQueries.NewsCategoryListing>> ListAsync(
        Flipper flipper,
        Option<string> search = default,
        Option<string> letter = default,
        DeletionState state = DeletionState.Active)
    {
        await using var ctx = _bed.Read();

        return await new NewsCategoryQueries(ctx).List(flipper, search, letter, state);
    }

    private static IReadOnlyList<string> Names(PagedResult<INewsCategoryQueries.NewsCategoryListing> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Name).ToList());

    private async Task EditAsync(Guid key, string name)
    {
        await using var ctx = _bed.Write();
        await new NewsCategoryCommands(ctx).Update(key, new INewsCategoryCommands.NewsCategoryDraft(name, None));
    }

    [Fact]
    public async Task List_ReturnsTheCategoriesByName()
    {
        await _bed.CreateCategoryAsync("beta");
        await _bed.CreateCategoryAsync("Alpha");

        var page = await ListAsync(new Flipper(1, 10));

        Assert.Equal(["Alpha", "beta"], Names(page));
        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task List_ShowsOnlyTheLatestVersionOfEachCategory()
    {
        var key = await _bed.CreateCategoryAsync("Old name");
        await EditAsync(key, "New name");

        var page = await ListAsync(new Flipper(1, 10));

        Assert.Equal(["New name"], Names(page));
        Assert.Equal(1, page.Total);
        var item = Assert.Single(page.Results.Match(() => [], items => items.ToList()));
        Assert.Equal(new VersionedKey<Guid>(key, 2), item.Id);
    }

    [Fact]
    public async Task List_IsPaginated()
    {
        foreach (var name in new[] { "A", "B", "C" })
        {
            await _bed.CreateCategoryAsync(name);
        }

        Assert.Equal(["A", "B"], Names(await ListAsync(new Flipper(1, 2))));
        Assert.Equal(["C"], Names(await ListAsync(new Flipper(2, 2))));
    }

    [Fact]
    public async Task List_CarriesTheHeaderImage()
    {
        var imageId = await _bed.AddImageAsync();
        var key = await _bed.CreateCategoryAsync("Releases", imageId);

        var item = Assert.Single((await ListAsync(new Flipper(1, 10))).Results.Match(() => [], items => items.ToList()));

        Assert.Equal((new VersionedKey<Guid>(key, 1), Some(imageId)), (item.Id, item.HeaderImageId));
    }

    [Fact]
    public async Task List_SearchesAndFiltersByLetterOnTheLatestName()
    {
        await _bed.CreateCategoryAsync("Releases");
        var events = await _bed.CreateCategoryAsync("Events");
        await EditAsync(events, "Happenings");

        Assert.Equal(["Happenings"], Names(await ListAsync(new Flipper(1, 10), search: "happen")));
        Assert.Empty(Names(await ListAsync(new Flipper(1, 10), search: "events")));
        Assert.Equal(["Releases"], Names(await ListAsync(new Flipper(1, 10), letter: "R")));
    }

    [Fact]
    public async Task List_SeparatesActiveFromDeletedCategories()
    {
        await _bed.CreateCategoryAsync("Alive");
        var gone = await _bed.CreateCategoryAsync("Gone");
        await using (var ctx = _bed.Write())
        {
            await new NewsCategoryCommands(ctx).Delete(gone);
        }

        Assert.Equal(["Alive"], Names(await ListAsync(new Flipper(1, 10))));
        Assert.Equal(["Gone"], Names(await ListAsync(new Flipper(1, 10), state: DeletionState.Deleted)));
    }

    [Fact]
    public async Task List_NoCategories_ReturnsAnEmptyPage()
    {
        var page = await ListAsync(new Flipper(1, 10));

        Assert.Equal((0L, 0L, None), (page.Total, page.PageCount, page.Results));
    }

    [Fact]
    public async Task GetLetters_ListsTheFirstLettersOfTheLatestNames()
    {
        await _bed.CreateCategoryAsync("Releases");
        await _bed.CreateCategoryAsync("Events");
        await using var ctx = _bed.Read();

        Assert.Equal(["E", "R"], await new NewsCategoryQueries(ctx).GetLetters(None, DeletionState.Active));
    }

    [Fact]
    public async Task ListActive_ReturnsEveryActiveCategoryWithoutPagingUsingTheLatestVersion()
    {
        foreach (var index in Enumerable.Range(1, 15))
        {
            await _bed.CreateCategoryAsync($"Category {index:D2}");
        }

        var gone = await _bed.CreateCategoryAsync("Gone");
        await using (var ctx = _bed.Write())
        {
            await new NewsCategoryCommands(ctx).Delete(gone);
        }

        await using var read = _bed.Read();
        var active = await new NewsCategoryQueries(read).ListActive();

        Assert.Equal(15, active.Count);
        Assert.DoesNotContain(active, category => category.Name == "Gone");
        Assert.Equal("Category 01", active[0].Name);
    }
}
