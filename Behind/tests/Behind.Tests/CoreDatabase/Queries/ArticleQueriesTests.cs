using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class ArticleQueriesTests : IDisposable
{
    private static readonly Flipper FirstPage = new(1, 10);

    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<PagedResult<IArticleQueries.ArticleListing>> ListAsync(
        Flipper? flipper = null,
        Option<Guid> category = default,
        Option<string> search = default)
    {
        await using var ctx = _bed.Read();

        return await new ArticleQueries(ctx).List(flipper ?? FirstPage, category, search);
    }

    private async Task<PagedResult<IArticleQueries.ManagedArticleListing>> ManagedAsync(
        Option<string> search = default,
        Option<string> letter = default,
        DeletionState state = DeletionState.Active)
    {
        await using var ctx = _bed.Read();

        return await new ArticleQueries(ctx).ListManaged(FirstPage, search, letter, state);
    }

    private async Task<Option<T>> QueryAsync<T>(Func<ArticleQueries, Task<Option<T>>> action)
    {
        await using var ctx = _bed.Read();

        return await action(new ArticleQueries(ctx));
    }

    private static IReadOnlyList<string> Titles(PagedResult<IArticleQueries.ArticleListing> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Title).ToList());

    private static IReadOnlyList<string> ManagedTitles(PagedResult<IArticleQueries.ManagedArticleListing> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Title).ToList());

    [Fact]
    public async Task List_ReturnsTheNewestCreatedFirst()
    {
        await _bed.CreateArticleAsync("Old", at: NewsTestBed.Now);
        await _bed.CreateArticleAsync("Newest", at: NewsTestBed.Now.AddDays(2));
        await _bed.CreateArticleAsync("Middle", at: NewsTestBed.Now.AddDays(1));

        var page = await ListAsync();

        Assert.Equal(["Newest", "Middle", "Old"], Titles(page));
        Assert.Equal((3L, 1L), (page.Total, page.PageCount));
    }

    [Fact]
    public async Task List_OnlyReturnsPublishedArticlesShowingTheLiveVersion()
    {
        await _bed.CreateArticleAsync("Draft", publish: false);
        var live = await _bed.SaveArticleAsync(null, "Live v1");
        await _bed.SaveArticleAsync(live.Key, "Live v2 draft", publish: false);

        var items = (await ListAsync()).Results.Match(() => [], found => found.ToList());

        var item = Assert.Single(items);
        Assert.Equal(("Live v1", 1L), (item.Title, item.Id.Version));
    }

    [Fact]
    public async Task List_IsPaginated()
    {
        foreach (var index in Enumerable.Range(1, 5))
        {
            await _bed.CreateArticleAsync($"Article {index}", at: NewsTestBed.Now.AddHours(index));
        }

        Assert.Equal(["Article 5", "Article 4"], Titles(await ListAsync(new Flipper(1, 2))));
        Assert.Equal(["Article 1"], Titles(await ListAsync(new Flipper(3, 2))));
        Assert.Empty(Titles(await ListAsync(new Flipper(4, 2))));
    }

    [Fact]
    public async Task List_NoArticles_ReturnsAnEmptyPage()
    {
        var page = await ListAsync();

        Assert.Equal((0L, 0L, None), (page.Total, page.PageCount, page.Results));
    }

    [Fact]
    public async Task List_FiltersByCategory()
    {
        var tutorials = await _bed.CreateArticleCategoryAsync("Tutorials");
        var other = await _bed.CreateArticleCategoryAsync("Other");
        await _bed.CreateArticleAsync("Tutorial", categoryId: tutorials);
        await _bed.CreateArticleAsync("Other", categoryId: other);
        await _bed.CreateArticleAsync("Uncategorised");

        Assert.Equal(["Tutorial"], Titles(await ListAsync(category: tutorials)));
    }

    [Fact]
    public async Task List_SearchesTheTitleAndTheContentIgnoringCase()
    {
        await _bed.CreateArticleAsync("Installing the server", content: "<p>Download it</p>");
        await _bed.CreateArticleAsync("Backups", content: "<p>Run the INSTALLER nightly</p>");
        await _bed.CreateArticleAsync("Unrelated", content: "<p>Nothing</p>");

        Assert.Equal(["Backups", "Installing the server"], Titles(await ListAsync(search: Some("  install "))).Order().ToList());
        Assert.Equal(["Backups"], Titles(await ListAsync(search: Some("nightly"))));
        Assert.Empty(Titles(await ListAsync(search: Some("missing"))));
    }

    [Fact]
    public async Task List_SearchDoesNotLookAtTheSummary()
    {
        await _bed.CreateArticleAsync("Plain", summary: "<p>keyword</p>", content: "<p>body</p>");

        Assert.Empty(Titles(await ListAsync(search: Some("keyword"))));
    }

    [Fact]
    public async Task List_SearchOnlyLooksAtTheLiveVersion()
    {
        var first = await _bed.SaveArticleAsync(null, "Guide", content: "<p>old words</p>");
        await _bed.SaveArticleAsync(first.Key, "Guide", content: "<p>new words</p>", publish: false);

        Assert.Equal(["Guide"], Titles(await ListAsync(search: Some("old"))));
        Assert.Empty(Titles(await ListAsync(search: Some("new"))));
    }

    [Fact]
    public async Task List_BlankSearch_ReturnsEverything()
    {
        await _bed.CreateArticleAsync("One");

        Assert.Equal(["One"], Titles(await ListAsync(search: Some("   "))));
    }

    [Fact]
    public async Task List_CarriesTheShownVersionAuthorSummaryTimesAndCategory()
    {
        var category = await _bed.CreateArticleCategoryAsync("Tutorials", await _bed.AddImageAsync());
        var first = await _bed.SaveArticleAsync(null, "Setup", category, authorId: _bed.OtherAuthorId);
        await _bed.SaveArticleAsync(
            first.Key,
            "Setup 2",
            category,
            at: NewsTestBed.Now.AddHours(1),
            summary: "<p>newer</p>");

        var item = Assert.Single((await ListAsync()).Results.Match(() => [], found => found.ToList()));

        Assert.Equal((new VersionedKey<Guid>(first.Key, 2), "Setup 2", "<p>newer</p>"), (item.Id, item.Title, item.Summary));
        Assert.Equal("Test User", item.Author);
        Assert.Equal((Some(NewsTestBed.Now), Some(NewsTestBed.Now.AddHours(1))), (item.CreatedAt, item.UpdatedAt));
        Assert.Equal(category, item.Category.Match(() => Guid.Empty, found => found.Id));
    }

    [Fact]
    public async Task List_DoesNotShowDeletedArticlesOrDeletedCategories()
    {
        var category = await _bed.CreateArticleCategoryAsync();
        var deleted = await _bed.CreateArticleAsync("Deleted");
        await _bed.CreateArticleAsync("Kept", categoryId: category);
        await _bed.WithArticleCommandsAsync(commands => commands.Delete(deleted));
        await using (var ctx = _bed.Write())
        {
            await new ArticleCategoryCommands(ctx, Substitute.For<IVfsCommands>()).Delete(category);
        }

        var item = Assert.Single((await ListAsync()).Results.Match(() => [], found => found.ToList()));

        Assert.Equal("Kept", item.Title);
        Assert.Equal(None, item.Category);
    }

    [Fact]
    public async Task ListManaged_ShowsTheLatestVersionAndTheActiveVersion()
    {
        var first = await _bed.SaveArticleAsync(null, "Setup");
        await _bed.SaveArticleAsync(first.Key, "Setup 2", publish: false);
        await _bed.CreateArticleAsync("Draft", publish: false);

        var items = (await ManagedAsync()).Results.Match(() => [], found => found.ToList());

        var published = Assert.Single(items, item => item.Id == first.Key);
        Assert.Equal(("Setup 2", 2L, Some(1L)), (published.Title, published.LatestVersion, published.ActiveVersion));
        var draft = Assert.Single(items, item => item.Title == "Draft");
        Assert.Equal(None, draft.ActiveVersion);
    }

    [Fact]
    public async Task ListManaged_SearchesTheTitleAndTheContentOfTheLatestVersion()
    {
        await _bed.CreateArticleAsync("Installing", content: "<p>x</p>");
        await _bed.CreateArticleAsync("Backups", content: "<p>nightly</p>");
        await _bed.CreateArticleAsync("Draft only", content: "<p>nightly run</p>", publish: false);

        Assert.Equal(["Installing"], ManagedTitles(await ManagedAsync(search: Some("INSTALL"))));
        Assert.Equal(["Backups", "Draft only"], ManagedTitles(await ManagedAsync(search: Some("nightly"))).Order().ToList());
    }

    [Fact]
    public async Task ListManaged_FiltersByLetterAndListsTheLetters()
    {
        await _bed.CreateArticleAsync("Alpha");
        await _bed.CreateArticleAsync("Beta");
        await using var ctx = _bed.Read();

        var letters = await new ArticleQueries(ctx).GetLetters(None, DeletionState.Active);

        Assert.Equal(["A", "B"], letters);
        Assert.Equal(["Beta"], ManagedTitles(await ManagedAsync(letter: Some("B"))));
    }

    [Fact]
    public async Task ListManaged_DeletedState_ListsOnlyDeletedArticles()
    {
        var deleted = await _bed.CreateArticleAsync("Gone");
        await _bed.CreateArticleAsync("Here");
        await _bed.WithArticleCommandsAsync(commands => commands.Delete(deleted));

        Assert.Equal(["Gone"], ManagedTitles(await ManagedAsync(state: DeletionState.Deleted)));
        Assert.Equal(["Here"], ManagedTitles(await ManagedAsync()));
    }

    [Fact]
    public async Task Find_ReturnsTheLiveVersionWithItsContent()
    {
        var first = await _bed.SaveArticleAsync(null, "Live", content: "<p>body</p>");
        await _bed.SaveArticleAsync(first.Key, "Draft", publish: false);

        var detail = (await QueryAsync(queries => queries.Find(first.Key))).Match(() => throw new InvalidOperationException(), found => found);

        Assert.Equal((new VersionedKey<Guid>(first.Key, 1), "Live", "<p>body</p>"), (detail.Id, detail.Title, detail.Content));
        Assert.Equal("<p>body</p>", detail.DisplayContent);
    }

    [Fact]
    public async Task Find_DisplayContentFallsBackToTheSummary()
    {
        var key = await _bed.CreateArticleAsync(summary: "<p>only summary</p>", content: " ");

        var detail = (await QueryAsync(queries => queries.Find(key))).Match(() => throw new InvalidOperationException(), found => found);

        Assert.Equal("<p>only summary</p>", detail.DisplayContent);
    }

    [Fact]
    public async Task Find_DraftDeletedOrUnknownArticle_IsNone()
    {
        var draft = await _bed.CreateArticleAsync(publish: false);
        var deleted = await _bed.CreateArticleAsync();
        await _bed.WithArticleCommandsAsync(commands => commands.Delete(deleted));

        Assert.Equal(None, await QueryAsync(queries => queries.Find(draft)));
        Assert.Equal(None, await QueryAsync(queries => queries.Find(deleted)));
        Assert.Equal(None, await QueryAsync(queries => queries.Find(Guid.NewGuid())));
    }

    [Fact]
    public async Task FindHistory_ListsEveryVersionNewestFirstAndMarksTheActiveOne()
    {
        var first = await _bed.SaveArticleAsync(null, "v1", at: NewsTestBed.Now);
        await _bed.SaveArticleAsync(first.Key, "v2", at: NewsTestBed.Now.AddHours(1), publish: false);

        var history = (await QueryAsync(queries => queries.FindHistory(first.Key))).Match(() => throw new InvalidOperationException(), found => found);

        Assert.Equal(Some(1L), history.ActiveVersion);
        Assert.Equal(
            [(2L, "v2", false, Some(NewsTestBed.Now.AddHours(1))), (1L, "v1", true, Some(NewsTestBed.Now))],
            history.Versions.Select(version => (version.Version, version.Title, version.IsActive, version.CreatedAt)));
    }

    [Fact]
    public async Task FindHistory_UnknownOrDeletedArticle_IsNone()
    {
        var deleted = await _bed.CreateArticleAsync();
        await _bed.WithArticleCommandsAsync(commands => commands.Delete(deleted));

        Assert.Equal(None, await QueryAsync(queries => queries.FindHistory(deleted)));
        Assert.Equal(None, await QueryAsync(queries => queries.FindHistory(Guid.NewGuid())));
    }

    [Fact]
    public async Task FindVersion_ReturnsThatVersionEvenWhenItIsADraft()
    {
        var first = await _bed.SaveArticleAsync(null, "v1");
        await _bed.SaveArticleAsync(first.Key, "v2", publish: false);

        var detail = (await QueryAsync(queries => queries.FindVersion(new VersionedKey<Guid>(first.Key, 2))))
            .Match(() => throw new InvalidOperationException(), found => found);

        Assert.Equal("v2", detail.Title);
        Assert.Equal(None, await QueryAsync(queries => queries.FindVersion(new VersionedKey<Guid>(first.Key, 3))));
    }
}
