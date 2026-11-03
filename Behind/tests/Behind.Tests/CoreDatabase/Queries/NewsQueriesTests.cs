using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class NewsQueriesTests : IDisposable
{
    private static readonly Flipper FirstPage = new(1, 10);
    private static readonly DateTimeOffset Clock = NewsTestBed.Now.AddDays(10);

    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<PagedResult<INewsQueries.NewsListing>> ListAsync(
        Flipper? flipper = null,
        Option<Guid> category = default,
        DateTimeOffset? now = null,
        Option<string> letter = default)
    {
        await using var ctx = _bed.Read();

        return await new NewsQueries(ctx).List(flipper ?? FirstPage, category, letter, now ?? Clock);
    }

    private async Task<IReadOnlyList<string>> LiveLettersAsync(Option<Guid> category = default, DateTimeOffset? now = null)
    {
        await using var ctx = _bed.Read();

        return await new NewsQueries(ctx).GetLiveLetters(category, now ?? Clock);
    }

    private async Task<PagedResult<INewsQueries.ManagedNewsListing>> ManagedAsync(
        Flipper? flipper = null,
        Option<string> search = default,
        Option<string> letter = default,
        DeletionState state = DeletionState.Active)
    {
        await using var ctx = _bed.Read();

        return await new NewsQueries(ctx).ListManaged(flipper ?? FirstPage, search, letter, state);
    }

    private static IReadOnlyList<string> Titles(PagedResult<INewsQueries.NewsListing> page) =>
        page.Results.Match(() => [], items => items.Select(item => item.Title).ToList());

    private static IReadOnlyList<INewsQueries.NewsListing> Items(PagedResult<INewsQueries.NewsListing> page) =>
        page.Results.Match(() => [], items => items.ToList());

    private static IReadOnlyList<INewsQueries.ManagedNewsListing> ManagedItems(
        PagedResult<INewsQueries.ManagedNewsListing> page) =>
        page.Results.Match(() => [], items => items.ToList());

    [Fact]
    public async Task List_LetterShowsOnlyTheLiveNewsWhoseTitleStartsWithIt()
    {
        await _bed.CreateNewsAsync("Alpha", startAt: NewsTestBed.Now);
        await _bed.CreateNewsAsync("Beta", startAt: NewsTestBed.Now);
        await _bed.CreateNewsAsync("42 days", startAt: NewsTestBed.Now);

        Assert.Equal(["Alpha"], Titles(await ListAsync(letter: Some("A"))));
        Assert.Equal(["42 days"], Titles(await ListAsync(letter: Some("#"))));
        Assert.Empty(Titles(await ListAsync(letter: Some("Z"))));
        Assert.Equal(3, (await ListAsync()).Total);
    }

    [Fact]
    public async Task GetLiveLetters_ReturnsTheFirstLettersOfTheLiveNewsOfTheCategoryOnly()
    {
        var releases = await _bed.CreateCategoryAsync("Releases");
        await _bed.CreateNewsAsync("Alpha", releases, NewsTestBed.Now);
        await _bed.CreateNewsAsync("Beta", default, NewsTestBed.Now);
        await _bed.CreateNewsAsync("Gamma", releases, startAt: NewsTestBed.Now.AddDays(30));
        var draft = await _bed.CreateNewsAsync("Delta", publish: false);

        Assert.NotEqual(Guid.Empty, draft);
        Assert.Equal(["A", "B"], await LiveLettersAsync());
        Assert.Equal(["A"], await LiveLettersAsync(releases));
    }

    [Fact]
    public async Task List_ReturnsTheNewestStartFirst()
    {
        await _bed.CreateNewsAsync("Old", startAt: NewsTestBed.Now);
        await _bed.CreateNewsAsync("Newest", startAt: NewsTestBed.Now.AddDays(2));
        await _bed.CreateNewsAsync("Middle", startAt: NewsTestBed.Now.AddDays(1));

        var page = await ListAsync();

        Assert.Equal(["Newest", "Middle", "Old"], Titles(page));
        Assert.Equal((3L, 1L), (page.Total, page.PageCount));
    }

    [Fact]
    public async Task List_SameStartIsOrderedByTheCreationTime()
    {
        await _bed.CreateNewsAsync("Created first", at: NewsTestBed.Now, startAt: NewsTestBed.Now);
        await _bed.CreateNewsAsync("Created last", at: NewsTestBed.Now.AddHours(1), startAt: NewsTestBed.Now);

        Assert.Equal(["Created last", "Created first"], Titles(await ListAsync()));
    }

    [Fact]
    public async Task List_IsPaginated()
    {
        foreach (var index in Enumerable.Range(1, 5))
        {
            await _bed.CreateNewsAsync($"News {index}", startAt: NewsTestBed.Now.AddHours(index));
        }

        Assert.Equal(["News 5", "News 4"], Titles(await ListAsync(new Flipper(1, 2))));
        Assert.Equal(["News 1"], Titles(await ListAsync(new Flipper(3, 2))));
        Assert.Empty(Titles(await ListAsync(new Flipper(4, 2))));
        var first = await ListAsync(new Flipper(1, 2));
        Assert.Equal((5L, 3L, 2), (first.Total, first.PageCount, first.PageSize));
    }

    [Fact]
    public async Task List_NoNews_ReturnsAnEmptyPage()
    {
        var page = await ListAsync();

        Assert.Equal((0L, 0L, None), (page.Total, page.PageCount, page.Results));
    }

    [Fact]
    public async Task List_CarriesTheShownVersionAuthorSummaryTimesCategoryCommentsAndSchedule()
    {
        var category = await _bed.CreateCategoryAsync("Releases", await _bed.AddImageAsync());
        var first = await _bed.SaveNewsAsync(null, "Launch", category, authorId: _bed.OtherAuthorId, startAt: NewsTestBed.Now);
        var second = await _bed.SaveNewsAsync(
            first.Key,
            "Launch 2",
            category,
            at: NewsTestBed.Now.AddHours(1),
            summary: "<p>newer</p>",
            highlighted: true,
            startAt: NewsTestBed.Now,
            endAt: NewsTestBed.Now.AddDays(20));
        await _bed.AddCommentAsync(first.Key);
        await _bed.AddCommentAsync(first.Key);

        var item = Assert.Single(Items(await ListAsync()));

        Assert.Equal(second, item.Id);
        Assert.Equal(("Launch 2", "<p>newer</p>", "Test User"), (item.Title, item.Summary, item.Author));
        Assert.Equal((Some(NewsTestBed.Now), Some(NewsTestBed.Now.AddHours(1))), (item.CreatedAt, item.UpdatedAt));
        Assert.Equal((2L, true), (item.CommentCount, item.IsHighlighted));
        Assert.Equal((NewsTestBed.Now, Some(NewsTestBed.Now.AddDays(20))), (item.StartAt, item.EndAt));
        var found = item.Category.Match(() => throw new InvalidOperationException(), info => info);
        Assert.Equal((category, "Releases"), (found.Id, found.Name));
        Assert.NotEqual(None, found.HeaderImageId);
    }

    [Fact]
    public async Task List_ShowsOnlyTheLiveVersionNotNewerDraftsAndNotNewsThatWasNeverPublished()
    {
        var first = await _bed.SaveNewsAsync(null, "Live version");
        await _bed.SaveNewsAsync(first.Key, "Newer draft", publish: false);
        await _bed.CreateNewsAsync("Never published", publish: false);

        var item = Assert.Single(Items(await ListAsync()));

        Assert.Equal(("Live version", 1L), (item.Title, item.Id.Version));
    }

    [Fact]
    public async Task List_AfterMakingAnOlderVersionLive_ShowsThatVersion()
    {
        var first = await _bed.SaveNewsAsync(null, "First");
        await _bed.SaveNewsAsync(first.Key, "Second");
        await _bed.WithNewsCommandsAsync(commands => commands.Activate(first));

        Assert.Equal(["First"], Titles(await ListAsync()));
    }

    [Fact]
    public async Task List_FiltersByCategory()
    {
        var releases = await _bed.CreateCategoryAsync("Releases");
        var events = await _bed.CreateCategoryAsync("Events");
        await _bed.CreateNewsAsync("Version 2", releases);
        await _bed.CreateNewsAsync("Meetup", events);
        await _bed.CreateNewsAsync("Uncategorized");

        var filtered = await ListAsync(category: releases);

        Assert.Equal(["Version 2"], Titles(filtered));
        Assert.Equal(1, filtered.Total);
    }

    [Fact]
    public async Task List_NewsOfADeletedCategory_HasNoCategory()
    {
        var category = await _bed.CreateCategoryAsync("Releases");
        await _bed.CreateNewsAsync("Version 2", category);
        await using (var ctx = _bed.Write())
        {
            await new NewsCategoryCommands(ctx, Substitute.For<IVfsCommands>()).Delete(category);
        }

        Assert.Equal(None, Assert.Single(Items(await ListAsync())).Category);
    }

    [Fact]
    public async Task List_ShowsTheCurrentNameOfTheCategory()
    {
        var category = await _bed.CreateCategoryAsync("Releases");
        await _bed.CreateNewsAsync("Version 2", category);
        await using (var ctx = _bed.Write())
        {
            await new NewsCategoryCommands(ctx, Substitute.For<IVfsCommands>()).Update(category, new INewsCategoryCommands.NewsCategoryDraft("Announcements", None));
        }

        var found = Assert.Single(Items(await ListAsync())).Category.Match(() => throw new InvalidOperationException(), info => info);

        Assert.Equal("Announcements", found.Name);
    }

    [Fact]
    public async Task List_OnlyShowsNewsWhoseScheduleIsRunning()
    {
        await _bed.CreateNewsAsync("Live", startAt: Clock.AddDays(-1));
        await _bed.CreateNewsAsync("Live until later", startAt: Clock.AddDays(-1), endAt: Clock.AddDays(1));
        await _bed.CreateNewsAsync("Scheduled", startAt: Clock.AddDays(1));
        await _bed.CreateNewsAsync("Expired", startAt: Clock.AddDays(-3), endAt: Clock.AddDays(-1));

        Assert.Equal(["Live until later", "Live"], Titles(await ListAsync()));
    }

    [Fact]
    public async Task List_TheScheduleBoundariesAreInclusiveStartExclusiveEnd()
    {
        await _bed.CreateNewsAsync("Starts now", startAt: Clock);
        await _bed.CreateNewsAsync("Ends now", startAt: Clock.AddDays(-1), endAt: Clock);

        Assert.Equal(["Starts now"], Titles(await ListAsync()));
    }

    [Fact]
    public async Task List_TotalAndPagingCountOnlyTheNewsThatIsShown()
    {
        await _bed.CreateNewsAsync("Live 1", startAt: Clock.AddDays(-2));
        await _bed.CreateNewsAsync("Live 2", startAt: Clock.AddDays(-1));
        await _bed.CreateNewsAsync("Scheduled", startAt: Clock.AddDays(1));

        var page = await ListAsync(new Flipper(1, 1));

        Assert.Equal((2L, 2L), (page.Total, page.PageCount));
    }

    [Fact]
    public async Task List_PutsTheHighlightedNewsFirstEvenWhenItStartedEarlier()
    {
        await _bed.CreateNewsAsync("Oldest star", startAt: NewsTestBed.Now.AddDays(-5), highlighted: true);
        await _bed.CreateNewsAsync("Newest", startAt: NewsTestBed.Now.AddDays(2));
        await _bed.CreateNewsAsync("Middle", startAt: NewsTestBed.Now.AddDays(1));

        var page = await ListAsync();

        Assert.Equal(["Oldest star", "Newest", "Middle"], Titles(page));
        Assert.Equal([true, false, false], Items(page).Select(item => item.IsHighlighted));
    }

    [Fact]
    public async Task List_TheHighlightedNewsIsOnlyOnTheFirstPage()
    {
        await _bed.CreateNewsAsync("Star", startAt: NewsTestBed.Now.AddDays(-5), highlighted: true);
        await _bed.CreateNewsAsync("Newer", startAt: NewsTestBed.Now.AddHours(2));
        await _bed.CreateNewsAsync("Newest", startAt: NewsTestBed.Now.AddHours(3));

        Assert.Equal(["Star", "Newest"], Titles(await ListAsync(new Flipper(1, 2))));
        Assert.Equal(["Newer"], Titles(await ListAsync(new Flipper(2, 2))));
    }

    [Fact]
    public async Task List_HidesDeletedNewsAndCountsOnlyActiveLatestComments()
    {
        var kept = await _bed.CreateNewsAsync("Kept");
        var gone = await _bed.CreateNewsAsync("Gone");
        await _bed.WithNewsCommandsAsync(commands => commands.Delete(gone));
        var edited = await _bed.AddCommentAsync(kept);
        await _bed.AddCommentAsync(kept);
        var removed = await _bed.AddCommentAsync(kept);
        await using (var ctx = _bed.Write())
        {
            var commands = new NewsCommentCommands(ctx, TimeProvider.System);
            await commands.Update(edited, "edited");
            await commands.Delete(removed);
        }

        var item = Assert.Single(Items(await ListAsync()));

        Assert.Equal(("Kept", 2L), (item.Title, item.CommentCount));
    }

    [Fact]
    public async Task ListManaged_ShowsTheLatestVersionWithTheLiveVersionAndTheNewerDraftInfo()
    {
        var first = await _bed.SaveNewsAsync(null, "Live title", startAt: NewsTestBed.Now, highlighted: true);
        await _bed.SaveNewsAsync(first.Key, "Draft title", publish: false);

        var item = Assert.Single(ManagedItems(await ManagedAsync()));

        Assert.Equal((first.Key, 2L, Some(1L), "Draft title"), (item.Id, item.LatestVersion, item.ActiveVersion, item.Title));
        var live = item.Live.Match(() => throw new InvalidOperationException(), info => info);
        Assert.Equal((NewsTestBed.Now, true), (live.StartAt, live.Highlighted));
    }

    [Fact]
    public async Task ListManaged_ANewsThatWasNeverPublishedHasNoLiveInfo()
    {
        await _bed.CreateNewsAsync("Draft", publish: false);

        var item = Assert.Single(ManagedItems(await ManagedAsync()));

        Assert.Equal((None, None), (item.ActiveVersion, item.Live));
    }

    [Fact]
    public async Task ListManaged_CountsNewsNotVersionsAndShowsScheduledAndExpiredNews()
    {
        var first = await _bed.SaveNewsAsync(null, "Many versions");
        await _bed.SaveNewsAsync(first.Key, "Many versions 2");
        await _bed.CreateNewsAsync("Scheduled", startAt: Clock.AddDays(1));
        await _bed.CreateNewsAsync("Expired", startAt: Clock.AddDays(-3), endAt: Clock.AddDays(-1));

        var page = await ManagedAsync();

        Assert.Equal(3, page.Total);
        Assert.Equal(3, ManagedItems(page).Count);
    }

    [Fact]
    public async Task ListManaged_SearchesLettersAndPages()
    {
        await _bed.CreateNewsAsync("Alpha", startAt: NewsTestBed.Now.AddDays(1));
        await _bed.CreateNewsAsync("Beta", startAt: NewsTestBed.Now);
        await _bed.CreateNewsAsync("42 things", startAt: NewsTestBed.Now.AddDays(-1));

        Assert.Equal(["Beta"], ManagedItems(await ManagedAsync(search: "ET")).Select(item => item.Title));
        Assert.Equal(["Alpha"], ManagedItems(await ManagedAsync(letter: "A")).Select(item => item.Title));
        Assert.Equal(["Alpha", "Beta"], ManagedItems(await ManagedAsync(new Flipper(1, 2))).Select(item => item.Title));
        Assert.Equal(["42 things"], ManagedItems(await ManagedAsync(new Flipper(2, 2))).Select(item => item.Title));
    }

    [Fact]
    public async Task ListManaged_SeparatesActiveFromDeletedNews()
    {
        await _bed.CreateNewsAsync("Alive");
        var gone = await _bed.CreateNewsAsync("Gone");
        await _bed.WithNewsCommandsAsync(commands => commands.Delete(gone));

        Assert.Equal(["Alive"], ManagedItems(await ManagedAsync()).Select(item => item.Title));
        Assert.Equal(["Gone"], ManagedItems(await ManagedAsync(state: DeletionState.Deleted)).Select(item => item.Title));
    }

    [Fact]
    public async Task ListManaged_CarriesTheTimesCategoryAndComments()
    {
        var category = await _bed.CreateCategoryAsync("Releases");
        var first = await _bed.SaveNewsAsync(null, "Launch", category);
        await _bed.SaveNewsAsync(first.Key, "Launch 2", category, at: NewsTestBed.Now.AddHours(2));
        await _bed.AddCommentAsync(first.Key, at: NewsTestBed.Now.AddHours(3));

        var item = Assert.Single(ManagedItems(await ManagedAsync()));

        Assert.Equal((Some(NewsTestBed.Now), Some(NewsTestBed.Now.AddHours(2)), 1L), (item.CreatedAt, item.UpdatedAt, item.CommentCount));
        Assert.Equal("Releases", item.Category.Match(() => string.Empty, found => found.Name));
    }

    [Fact]
    public async Task GetLetters_ListsTheFirstLettersOfTheLatestTitles()
    {
        await _bed.CreateNewsAsync("Alpha");
        await _bed.CreateNewsAsync("Beta");
        await _bed.CreateNewsAsync("42 things");
        await using var ctx = _bed.Read();

        var letters = await new NewsQueries(ctx).GetLetters(None, DeletionState.Active);

        Assert.Equal(["A", "B", "#"], letters);
    }

    [Fact]
    public async Task Find_ReturnsTheLiveVersionInFull()
    {
        var category = await _bed.CreateCategoryAsync("Releases");
        var first = await _bed.SaveNewsAsync(null, "Launch", category, summary: "<p>s</p>", content: "<p>c</p>");
        await _bed.SaveNewsAsync(first.Key, "Draft", publish: false);
        await using var ctx = _bed.Read();

        var found = (await new NewsQueries(ctx).Find(first.Key, Clock)).Match(() => throw new InvalidOperationException(), news => news);

        Assert.Equal((first, "Launch", "<p>s</p>", "<p>c</p>", "Test User"), (found.Id, found.Title, found.Summary, found.Content, found.Author));
        Assert.Equal((Some(NewsTestBed.Now), None), (found.CreatedAt, found.UpdatedAt));
        Assert.Equal("Releases", found.Category.Match(() => string.Empty, info => info.Name));
    }

    [Fact]
    public async Task Find_HidesDraftsDeletedScheduledAndExpiredNews()
    {
        var draft = await _bed.CreateNewsAsync("Draft", publish: false);
        var deleted = await _bed.CreateNewsAsync("Deleted");
        await _bed.WithNewsCommandsAsync(commands => commands.Delete(deleted));
        var scheduled = await _bed.CreateNewsAsync("Scheduled", startAt: Clock.AddDays(1));
        var expired = await _bed.CreateNewsAsync("Expired", startAt: Clock.AddDays(-3), endAt: Clock.AddDays(-1));
        await using var ctx = _bed.Read();
        var queries = new NewsQueries(ctx);

        foreach (var key in new[] { draft, deleted, scheduled, expired, Guid.NewGuid() })
        {
            Assert.Equal(None, await queries.Find(key, Clock));
        }
    }

    [Fact]
    public async Task Find_NewsWithoutContent_DisplaysTheSummary()
    {
        var key = await _bed.CreateNewsAsync("Short only", summary: "<p>only short</p>", content: "");
        await using var ctx = _bed.Read();

        var found = (await new NewsQueries(ctx).Find(key, Clock)).Match(() => throw new InvalidOperationException(), news => news);

        Assert.Equal(("", "<p>only short</p>"), (found.Content, found.DisplayContent));
    }

    [Fact]
    public async Task FindHistory_ListsEveryVersionNewestFirstWithTheLiveOneMarked()
    {
        var first = await _bed.SaveNewsAsync(null, "First", at: NewsTestBed.Now);
        await _bed.SaveNewsAsync(first.Key, "Second", at: NewsTestBed.Now.AddHours(1), highlighted: true, publish: false);
        await using var ctx = _bed.Read();

        var history = (await new NewsQueries(ctx).FindHistory(first.Key)).Match(() => throw new InvalidOperationException(), found => found);

        Assert.Equal((first.Key, Some(1L)), (history.Id, history.ActiveVersion));
        Assert.Equal([2L, 1L], history.Versions.Select(version => version.Version));
        Assert.Equal(["Second", "First"], history.Versions.Select(version => version.Title));
        Assert.Equal([false, true], history.Versions.Select(version => version.IsActive));
        Assert.Equal([true, false], history.Versions.Select(version => version.Highlighted));
        Assert.Equal([Some(NewsTestBed.Now.AddHours(1)), Some(NewsTestBed.Now)], history.Versions.Select(version => version.CreatedAt));
    }

    [Fact]
    public async Task FindHistory_ADraftOnlyNewsHasNoActiveVersionAndUnknownOrDeletedNewsHasNoHistory()
    {
        var draft = await _bed.CreateNewsAsync(publish: false);
        var deleted = await _bed.CreateNewsAsync();
        await _bed.WithNewsCommandsAsync(commands => commands.Delete(deleted));
        await using var ctx = _bed.Read();
        var queries = new NewsQueries(ctx);

        Assert.Equal(None, (await queries.FindHistory(draft)).Match(() => None, history => history.ActiveVersion));
        Assert.Equal(None, await queries.FindHistory(Guid.NewGuid()));
        Assert.Equal(None, await queries.FindHistory(deleted));
    }

    [Fact]
    public async Task FindVersion_ReturnsAnyVersionIncludingOnesThatAreNotLive()
    {
        var first = await _bed.SaveNewsAsync(null, "First");
        var second = await _bed.SaveNewsAsync(first.Key, "Second", publish: false, startAt: NewsTestBed.Now.AddDays(1));
        await using var ctx = _bed.Read();
        var queries = new NewsQueries(ctx);

        var found = (await queries.FindVersion(second)).Match(() => throw new InvalidOperationException(), news => news);

        Assert.Equal((second, "Second", NewsTestBed.Now.AddDays(1)), (found.Id, found.Title, found.StartAt));
        Assert.Equal(None, await queries.FindVersion(new VersionedKey<Guid>(first.Key, 9)));
    }

    [Fact]
    public async Task List_NewsWithoutASchedule_IsAlwaysShownAndOrderedByItsCreationAmongScheduledNews()
    {
        await _bed.CreateNewsAsync("Unscheduled old", at: NewsTestBed.Now);
        await _bed.CreateNewsAsync("Scheduled", at: NewsTestBed.Now, startAt: NewsTestBed.Now.AddDays(1));
        await _bed.CreateNewsAsync("Unscheduled new", at: NewsTestBed.Now.AddHours(2));

        Assert.Equal(["Scheduled", "Unscheduled new", "Unscheduled old"], Titles(await ListAsync(now: NewsTestBed.Now.AddDays(2))));
        Assert.Equal(["Unscheduled new", "Unscheduled old"], Titles(await ListAsync(now: NewsTestBed.Now.AddHours(3))));
        Assert.Equal(3, (await ListAsync(now: NewsTestBed.Now.AddYears(5))).Total);
    }

    [Fact]
    public async Task List_AnEndWithoutAStartIsNotPossibleButAStartOnlyScheduleNeverEnds()
    {
        await _bed.CreateNewsAsync("Starts", startAt: Clock.AddDays(-1));

        Assert.Equal(["Starts"], Titles(await ListAsync(now: Clock.AddYears(20))));
    }

    [Fact]
    public async Task List_CarriesNoScheduleForUnscheduledNews()
    {
        await _bed.CreateNewsAsync("Plain");

        var item = Assert.Single(Items(await ListAsync()));

        Assert.Equal((None, None), (item.StartAt, item.EndAt));
    }

    [Fact]
    public async Task ListManaged_UnscheduledLiveNewsHasALiveInfoWithoutSchedule()
    {
        await _bed.CreateNewsAsync("Plain");

        var item = Assert.Single(ManagedItems(await ManagedAsync()));

        var live = item.Live.Match(() => throw new InvalidOperationException(), info => info);
        Assert.Equal((None, None), (live.StartAt, live.EndAt));
    }

    [Fact]
    public async Task Find_UnscheduledNews_IsFoundAtAnyTime()
    {
        var key = await _bed.CreateNewsAsync("Plain");
        await using var ctx = _bed.Read();
        var queries = new NewsQueries(ctx);

        Assert.NotEqual(None, await queries.Find(key, NewsTestBed.Now.AddYears(30)));
        var found = (await queries.Find(key, Clock)).Match(() => throw new InvalidOperationException(), news => news);
        Assert.Equal((None, None), (found.StartAt, found.EndAt));
    }

    [Fact]
    public async Task FindHistory_ShowsWhichVersionsAreScheduled()
    {
        var first = await _bed.SaveNewsAsync(null, "Plain");
        await _bed.SaveNewsAsync(first.Key, "Timed", startAt: NewsTestBed.Now.AddDays(1), endAt: NewsTestBed.Now.AddDays(2));
        await using var ctx = _bed.Read();

        var history = (await new NewsQueries(ctx).FindHistory(first.Key)).Match(() => throw new InvalidOperationException(), found => found);

        Assert.Equal([Some(NewsTestBed.Now.AddDays(1)), None], history.Versions.Select(version => version.StartAt));
        Assert.Equal([Some(NewsTestBed.Now.AddDays(2)), None], history.Versions.Select(version => version.EndAt));
    }
}
