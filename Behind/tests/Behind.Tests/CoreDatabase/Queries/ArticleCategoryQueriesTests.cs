using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class ArticleCategoryQueriesTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<PagedResult<IArticleCategoryQueries.ArticleCategoryListing>> ListAsync(
        Flipper flipper,
        Option<string> search = default,
        Option<string> letter = default,
        DeletionState state = DeletionState.Active)
    {
        await using var ctx = _bed.Read();

        return await new ArticleCategoryQueries(ctx).List(flipper, search, letter, state);
    }

    private static IReadOnlyList<string> Names(PagedResult<IArticleCategoryQueries.ArticleCategoryListing> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Name).ToList());

    [Fact]
    public async Task List_ReturnsTheCategoriesByNameWithoutTheNewsCategories()
    {
        await _bed.CreateArticleCategoryAsync("beta");
        await _bed.CreateArticleCategoryAsync("Alpha");
        await _bed.CreateCategoryAsync("News only");

        var page = await ListAsync(new Flipper(1, 10));

        Assert.Equal(["Alpha", "beta"], Names(page));
        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task List_ShowsOnlyTheLatestVersionOfEachCategory()
    {
        var key = await _bed.CreateArticleCategoryAsync("Old name");
        await using (var ctx = _bed.Write())
        {
            await new ArticleCategoryCommands(ctx).Update(
                key,
                new IArticleCategoryCommands.ArticleCategoryDraft("New name", None));
        }

        var page = await ListAsync(new Flipper(1, 10));

        var item = Assert.Single(page.Results.Match(() => [], items => items.ToList()));
        Assert.Equal((new VersionedKey<Guid>(key, 2), "New name"), (item.Id, item.Name));
    }

    [Fact]
    public async Task List_IsPaginatedSearchedAndFilteredByLetter()
    {
        foreach (var name in new[] { "Apple", "Avocado", "Banana" })
        {
            await _bed.CreateArticleCategoryAsync(name);
        }

        Assert.Equal(["Apple"], Names(await ListAsync(new Flipper(1, 1))));
        Assert.Equal(["Avocado"], Names(await ListAsync(new Flipper(2, 1))));
        Assert.Equal(["Banana"], Names(await ListAsync(new Flipper(1, 10), search: Some("NAN"))));
        Assert.Equal(["Apple", "Avocado"], Names(await ListAsync(new Flipper(1, 10), letter: Some("A"))));
    }

    [Fact]
    public async Task List_DeletedState_ListsOnlyDeletedCategories()
    {
        var gone = await _bed.CreateArticleCategoryAsync("Gone");
        await _bed.CreateArticleCategoryAsync("Here");
        await using (var ctx = _bed.Write())
        {
            await new ArticleCategoryCommands(ctx).Delete(gone);
        }

        Assert.Equal(["Gone"], Names(await ListAsync(new Flipper(1, 10), state: DeletionState.Deleted)));
        Assert.Equal(["Here"], Names(await ListAsync(new Flipper(1, 10))));
    }

    [Fact]
    public async Task GetLetters_ListsTheFirstLettersOfTheLatestNames()
    {
        await _bed.CreateArticleCategoryAsync("Beta");
        await _bed.CreateArticleCategoryAsync("alpha");
        await using var ctx = _bed.Read();

        var letters = await new ArticleCategoryQueries(ctx).GetLetters(None, DeletionState.Active);

        Assert.Equal(["A", "B"], letters);
    }

    [Fact]
    public async Task ListActive_ReturnsTheActiveCategoriesWithTheirHeaderImage()
    {
        var image = await _bed.AddImageAsync();
        await _bed.CreateArticleCategoryAsync("With image", image);
        var gone = await _bed.CreateArticleCategoryAsync("Gone");
        await using (var ctx = _bed.Write())
        {
            await new ArticleCategoryCommands(ctx).Delete(gone);
        }
        await using var read = _bed.Read();

        var active = await new ArticleCategoryQueries(read).ListActive();

        var item = Assert.Single(active);
        Assert.Equal(("With image", Some(image)), (item.Name, item.HeaderImageId));
    }
}
