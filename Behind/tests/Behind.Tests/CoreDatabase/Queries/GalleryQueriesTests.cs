using Behind.Core.Database;
using Behind.Core.Database.Queries;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class GalleryQueriesTests : IDisposable
{
    private static readonly Flipper FirstPage = new(1, 10);

    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<PagedResult<IGalleryQueries.GalleryListing>> ListAsync(
        Flipper? flipper = null,
        Option<string> search = default)
    {
        await using var ctx = _bed.Read();

        return await new GalleryQueries(ctx).List(flipper ?? FirstPage, search);
    }

    private async Task<PagedResult<IGalleryQueries.ManagedGalleryListing>> ManagedAsync(
        Option<string> search = default,
        Option<string> letter = default,
        DeletionState state = DeletionState.Active)
    {
        await using var ctx = _bed.Read();

        return await new GalleryQueries(ctx).ListManaged(FirstPage, search, letter, state);
    }

    private async Task<Option<T>> QueryAsync<T>(Func<GalleryQueries, Task<Option<T>>> action)
    {
        await using var ctx = _bed.Read();

        return await action(new GalleryQueries(ctx));
    }

    private static T Found<T>(Option<T> option) =>
        option.Match<T>(() => throw new InvalidOperationException(), value => value);

    private static IReadOnlyList<string> Titles(PagedResult<IGalleryQueries.GalleryListing> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Title).ToList());

    private static IReadOnlyList<string> ManagedTitles(PagedResult<IGalleryQueries.ManagedGalleryListing> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Title).ToList());

    [Fact]
    public async Task List_ReturnsTheNewestCreatedFirst()
    {
        await _bed.CreateGalleryAsync("Old", at: NewsTestBed.Now);
        await _bed.CreateGalleryAsync("Newest", at: NewsTestBed.Now.AddDays(2));
        await _bed.CreateGalleryAsync("Middle", at: NewsTestBed.Now.AddDays(1));

        var page = await ListAsync();

        Assert.Equal(["Newest", "Middle", "Old"], Titles(page));
        Assert.Equal((3L, 1L), (page.Total, page.PageCount));
    }

    [Fact]
    public async Task List_OnlyShowsTheLiveVersionOfPublishedGalleries()
    {
        await _bed.CreateGalleryAsync("Draft only", publish: false);
        var live = await _bed.SaveGalleryAsync(null, "Live");
        await _bed.SaveGalleryAsync(live.Key, "Live newer draft", publish: false);

        Assert.Equal(["Live"], Titles(await ListAsync()));
    }

    [Fact]
    public async Task List_SkipsDeletedGalleries()
    {
        await _bed.CreateGalleryAsync("Kept");
        var gone = await _bed.CreateGalleryAsync("Gone");
        await _bed.WithGalleryCommandsAsync(commands => commands.Delete(gone));

        Assert.Equal(["Kept"], Titles(await ListAsync()));
    }

    [Fact]
    public async Task List_SearchesTheTitleAndTheDescriptionIgnoringCase()
    {
        await _bed.CreateGalleryAsync("Mountains", "Snow");
        await _bed.CreateGalleryAsync("Beach", "Sand and SNOW");
        await _bed.CreateGalleryAsync("City");

        Assert.Equal(["Beach", "Mountains"], Titles(await ListAsync(search: Some("snow"))).Order());
        Assert.Equal(["City"], Titles(await ListAsync(search: Some("CITY"))));
    }

    [Fact]
    public async Task List_PagesTheResults()
    {
        foreach (var day in Enumerable.Range(0, 3))
        {
            await _bed.CreateGalleryAsync($"Gallery {day}", at: NewsTestBed.Now.AddDays(day));
        }

        var second = await ListAsync(new Flipper(2, 2));

        Assert.Equal(["Gallery 0"], Titles(second));
        Assert.Equal((3L, 2L), (second.Total, second.PageCount));
    }

    [Fact]
    public async Task List_ShowsTheItemCountAuthorAndTimes()
    {
        var items = await _bed.NewGalleryItemsAsync("A", "B", "C");
        var key = await _bed.CreateGalleryAsync(items: items, authorId: _bed.OtherAuthorId);

        var listing = Assert.Single(((await ListAsync()).Results.Match(() => [], found => found)));

        Assert.Equal((key, 3), (listing.Id.Key, listing.ItemCount));
        Assert.Equal(Some(NewsTestBed.Now), listing.CreatedAt);
        Assert.NotEmpty(listing.Author);
    }

    [Fact]
    public async Task List_UsesTheUploadedThumbnailOtherwiseTheFirstItemThumbnail()
    {
        var uploaded = await _bed.AddGalleryImageAsync();
        var items = await _bed.NewGalleryItemsAsync("A", "B");
        await _bed.CreateGalleryAsync("Uploaded", thumbnailImageId: uploaded, at: NewsTestBed.Now);
        await _bed.CreateGalleryAsync("Generated", items: items, at: NewsTestBed.Now.AddDays(1));
        await _bed.CreateGalleryAsync("Empty", items: [], at: NewsTestBed.Now.AddDays(2));

        var listings = (await ListAsync()).Results.Match(() => [], found => found).ToList();

        Assert.Equal(None, listings[0].ThumbnailId);
        Assert.Equal(Some(items[0].ThumbnailId), listings[1].ThumbnailId);
        Assert.Equal(Some(uploaded), listings[2].ThumbnailId);
    }

    [Fact]
    public async Task ListManaged_ShowsTheLatestVersionAndTheLiveVersion()
    {
        var live = await _bed.SaveGalleryAsync(null, "Live");
        await _bed.SaveGalleryAsync(live.Key, "Live v2", publish: false);
        await _bed.CreateGalleryAsync("Draft", publish: false);

        var page = await ManagedAsync();

        Assert.Equal(["Draft", "Live v2"], ManagedTitles(page).Order());
        var listing = page.Results.Match(() => [], found => found).Single(item => item.Id == live.Key);
        Assert.Equal((2L, Some(1L)), (listing.LatestVersion, listing.ActiveVersion));
        var draft = page.Results.Match(() => [], found => found).Single(item => item.Title == "Draft");
        Assert.Equal(None, draft.ActiveVersion);
    }

    [Fact]
    public async Task ListManaged_ShowsDeletedGalleriesOnlyInTheDeletedState()
    {
        await _bed.CreateGalleryAsync("Kept");
        var gone = await _bed.CreateGalleryAsync("Gone");
        await _bed.WithGalleryCommandsAsync(commands => commands.Delete(gone));

        Assert.Equal(["Kept"], ManagedTitles(await ManagedAsync()));
        Assert.Equal(["Gone"], ManagedTitles(await ManagedAsync(state: DeletionState.Deleted)));
    }

    [Fact]
    public async Task ListManaged_FiltersByLetterAndSearch()
    {
        await _bed.CreateGalleryAsync("Alps");
        await _bed.CreateGalleryAsync("Beach", "Sand");

        Assert.Equal(["Alps"], ManagedTitles(await ManagedAsync(letter: Some("A"))));
        Assert.Equal(["Beach"], ManagedTitles(await ManagedAsync(search: Some("sand"))));
    }

    [Fact]
    public async Task GetLetters_ListsTheInitialsOfTheLatestTitles()
    {
        await _bed.CreateGalleryAsync("Alps");
        await _bed.CreateGalleryAsync("Beach");
        var gone = await _bed.CreateGalleryAsync("Gone");
        await _bed.WithGalleryCommandsAsync(commands => commands.Delete(gone));

        await using var ctx = _bed.Read();
        var queries = new GalleryQueries(ctx);

        Assert.Equal(["A", "B"], await queries.GetLetters(None, DeletionState.Active));
        Assert.Equal(["G"], await queries.GetLetters(None, DeletionState.Deleted));
    }

    [Fact]
    public async Task Find_ReturnsTheLiveVersionWithTheOrderedItems()
    {
        var items = await _bed.NewGalleryItemsAsync("First", "Second");
        var first = await _bed.SaveGalleryAsync(null, "Trip", "Summer", items);
        await _bed.SaveGalleryAsync(first.Key, "Trip draft", items: [], publish: false);

        var detail = Found(await QueryAsync(queries => queries.Find(first.Key)));

        Assert.Equal((first, "Trip", "Summer"), (detail.Id, detail.Title, detail.Description));
        Assert.Equal(
            [("First", items[0].ImageId, items[0].ThumbnailId, 0), ("Second", items[1].ImageId, items[1].ThumbnailId, 1)],
            detail.Items.Select(item => (item.Description, item.ImageId, item.ThumbnailId, item.Position)));
        Assert.True(detail.ThumbnailGenerated);
        Assert.Equal(Some(items[0].ThumbnailId), detail.ThumbnailId);
    }

    [Fact]
    public async Task Find_DraftOnlyDeletedOrUnknownGallery_ReturnsNone()
    {
        var draft = await _bed.CreateGalleryAsync(publish: false);
        var gone = await _bed.CreateGalleryAsync();
        await _bed.WithGalleryCommandsAsync(commands => commands.Delete(gone));

        Assert.Equal(None, await QueryAsync(queries => queries.Find(draft)));
        Assert.Equal(None, await QueryAsync(queries => queries.Find(gone)));
        Assert.Equal(None, await QueryAsync(queries => queries.Find(Guid.NewGuid())));
    }

    [Fact]
    public async Task FindVersion_ReturnsADraftWithItsOwnItems()
    {
        var first = await _bed.SaveGalleryAsync(null, "Trip", items: await _bed.NewGalleryItemsAsync("One"));
        var draft = await _bed.SaveGalleryAsync(
            first.Key,
            "Trip draft",
            items: await _bed.NewGalleryItemsAsync("Two", "Three"),
            publish: false);

        var detail = Found(await QueryAsync(queries => queries.FindVersion(draft)));

        Assert.Equal("Trip draft", detail.Title);
        Assert.Equal(["Two", "Three"], detail.Items.Select(item => item.Description));
        Assert.Equal(None, await QueryAsync(queries => queries.FindVersion(new VersionedKey<Guid>(first.Key, 9))));
    }

    [Fact]
    public async Task FindHistory_ListsTheVersionsNewestFirstWithTheLiveOne()
    {
        var first = await _bed.SaveGalleryAsync(null, "Trip", items: await _bed.NewGalleryItemsAsync("One"));
        await _bed.SaveGalleryAsync(
            first.Key,
            "Trip 2",
            items: await _bed.NewGalleryItemsAsync("Two", "Three"),
            publish: false,
            at: NewsTestBed.Now.AddDays(1));

        var history = Found(await QueryAsync(queries => queries.FindHistory(first.Key)));

        Assert.Equal(Some(1L), history.ActiveVersion);
        Assert.Equal(
            [(2L, "Trip 2", 2, false), (1L, "Trip", 1, true)],
            history.Versions.Select(item => (item.Version, item.Title, item.ItemCount, item.IsActive)));
        Assert.Equal(Some(NewsTestBed.Now.AddDays(1)), history.Versions[0].CreatedAt);
    }

    [Fact]
    public async Task FindHistory_UnknownGallery_ReturnsNone()
    {
        Assert.Equal(None, await QueryAsync(queries => queries.FindHistory(Guid.NewGuid())));
    }
}
