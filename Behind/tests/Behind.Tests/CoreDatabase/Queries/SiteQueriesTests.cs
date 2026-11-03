using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class SiteQueriesTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory = new();
    private readonly AuditActor _actor;

    public SiteQueriesTests()
    {
        using var seed = _factory.CreateContext();
        var actorId = DatabaseSeedHelper.SeedHumanUserAsync(seed, "actor@test.com").GetAwaiter().GetResult();
        DatabaseSeedHelper.EnsureBuiltInUsersAsync(seed).GetAwaiter().GetResult();
        _actor = AuditActor.ForUser(actorId);
    }

    public void Dispose() => _factory.Dispose();

    private async Task<VersionedKey<Guid>> SaveAsync(
        string title,
        string slug,
        bool publish,
        Guid? siteId = null,
        int minutes = 0,
        string content = "<p>content</p>")
    {
        await using var ctx = _factory.CreateContext();
        await using var versions = _factory.CreateVersioningContext();
        var commands = new SiteCommands(ctx, versions, new FixedTimeProvider(Now.AddMinutes(minutes)), Substitute.For<IVfsCommands>());
        var result = siteId is { } id
            ? await commands.SaveVersion(id, title, slug, content, publish)
            : await commands.Create(title, slug, content, publish);

        return Assert.IsType<SiteSaveResult.Saved>(result).Id;
    }

    private async Task<T> QueryAsync<T>(Func<SiteQueries, Task<T>> query)
    {
        await using var ctx = _factory.CreateContext();
        await using var versions = _factory.CreateVersioningContext();
        return await query(new SiteQueries(ctx, versions));
    }

    private static readonly Flipper FirstPage = new(1, 10);

    private async Task<IReadOnlyList<ISiteQueries.SiteListing>> ListAsync(
        Flipper? flipper = null,
        Option<string>? search = null,
        Option<string>? letter = null,
        DeletionState state = DeletionState.Active)
    {
        var page = await QueryAsync(queries => queries.List(
            flipper ?? FirstPage,
            search ?? None,
            letter ?? None,
            state));

        return page.Results.Match(() => (IReadOnlyList<ISiteQueries.SiteListing>)[], items => items.ToList());
    }

    private async Task DeleteAsync(Guid siteId)
    {
        await using var ctx = _factory.CreateAuditedContext(_actor);
        await using var versions = _factory.CreateAuditedVersioningContext(_actor);
        await new SiteCommands(ctx, versions, new FixedTimeProvider(Now), Substitute.For<IVfsCommands>()).Delete(siteId);
    }

    [Fact]
    public async Task List_ReturnsTheLatestVersionOfEverySiteAlphabetically()
    {
        var about = await SaveAsync("About", "about", true);
        await SaveAsync("About us", "about-us", false, about.Key, 1);
        await SaveAsync("Contact", "contact", false, minutes: 5);

        var sites = await ListAsync();

        Assert.Equal(["About us", "Contact"], sites.Select(site => site.Title));
        Assert.Equal(["about-us", "contact"], sites.Select(site => site.Slug));
        var listed = sites[0];
        Assert.Equal(about.Key, listed.Id);
        Assert.Equal(2L, listed.LatestVersion);
        Assert.Equal(Some(1L), listed.ActiveVersion);
        Assert.Equal(Some("about"), listed.ActiveSlug);
        Assert.Equal(Now.AddMinutes(1), listed.UpdatedAt);
        Assert.Equal(None, sites[1].ActiveVersion);
        Assert.Equal(None, sites[1].ActiveSlug);
    }

    [Fact]
    public async Task List_WithoutSites_IsEmpty()
    {
        var page = await QueryAsync(queries => queries.List(FirstPage, None, None, DeletionState.Active));

        Assert.Empty(await ListAsync());
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task List_PagesTheSites()
    {
        foreach (var name in new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo" })
        {
            await SaveAsync(name, name.ToLowerInvariant(), true);
        }

        var page = await QueryAsync(queries => queries.List(new Flipper(2, 2), None, None, DeletionState.Active));

        Assert.Equal(5, page.Total);
        Assert.Equal(3, page.PageCount);
        Assert.Equal(["Charlie", "Delta"], await ListTitlesAsync(new Flipper(2, 2)));
    }

    private async Task<IEnumerable<string>> ListTitlesAsync(Flipper flipper) =>
        (await ListAsync(flipper)).Select(site => site.Title);

    [Fact]
    public async Task List_SearchMatchesTheTitleOrTheSlugIgnoringCase()
    {
        await SaveAsync("About us", "company", true);
        await SaveAsync("Contact", "reach-out", true);

        Assert.Equal(["About us"], (await ListAsync(search: Some("ABOUT"))).Select(site => site.Title));
        Assert.Equal(["Contact"], (await ListAsync(search: Some("reach"))).Select(site => site.Title));
        Assert.Empty(await ListAsync(search: Some("nothing")));
    }

    [Fact]
    public async Task List_SearchOnlySeesTheLatestVersion()
    {
        var about = await SaveAsync("Old name", "about", true);
        await SaveAsync("New name", "about", false, about.Key, 1);

        Assert.Empty(await ListAsync(search: Some("old")));
        Assert.Single(await ListAsync(search: Some("new")));
    }

    [Fact]
    public async Task List_FiltersByTheFirstLetterOfTheTitle()
    {
        await SaveAsync("About", "about", true);
        await SaveAsync("Contact", "contact", true);
        await SaveAsync("42 things", "things", true);

        Assert.Equal(["About"], (await ListAsync(letter: Some("A"))).Select(site => site.Title));
        Assert.Equal(["42 things"], (await ListAsync(letter: Some("#"))).Select(site => site.Title));
    }

    [Fact]
    public async Task GetLetters_ListsTheLettersInUseInOrder()
    {
        await SaveAsync("Contact", "contact", true);
        await SaveAsync("About", "about", true);
        await SaveAsync("42 things", "things", true);

        var letters = await QueryAsync(queries => queries.GetLetters(None, DeletionState.Active));

        Assert.Equal(["A", "C", "#"], letters);
        Assert.Equal(["A"], await QueryAsync(queries => queries.GetLetters(Some("about"), DeletionState.Active)));
    }

    [Fact]
    public async Task List_HidesDeletedSitesAndListsThemInTheDeletedState()
    {
        var about = await SaveAsync("About", "about", true);
        await SaveAsync("About us", "about", false, about.Key, 1);
        await SaveAsync("Contact", "contact", true);
        await DeleteAsync(about.Key);

        var active = await ListAsync();
        var deleted = await ListAsync(state: DeletionState.Deleted);

        Assert.Equal(["Contact"], active.Select(site => site.Title));
        var gone = Assert.Single(deleted);
        Assert.Equal(about.Key, gone.Id);
        Assert.Equal("About us", gone.Title);
        Assert.Equal(2L, gone.LatestVersion);
        Assert.Equal(["A"], await QueryAsync(queries => queries.GetLetters(None, DeletionState.Deleted)));
    }

    [Fact]
    public async Task FindActiveBySlug_DeletedSite_IsNotServed()
    {
        var about = await SaveAsync("About", "about", true);
        await DeleteAsync(about.Key);

        Assert.Equal(None, await QueryAsync(queries => queries.FindActiveBySlug("about")));
        Assert.Equal(None, await QueryAsync(queries => queries.FindHistory(about.Key)));
    }

    [Fact]
    public async Task FindHistory_UnknownSite_ReturnsNone()
    {
        Assert.Equal(None, await QueryAsync(queries => queries.FindHistory(Guid.NewGuid())));
    }

    [Fact]
    public async Task FindVersion_ReturnsTheVersionedSite()
    {
        var about = await SaveAsync("About", "about", true, content: "<p>one</p>");
        var second = await SaveAsync("About", "about", false, about.Key, 1, "<p>two</p>");

        var site = (await QueryAsync(queries => queries.FindVersion(second))).Match(() => throw new InvalidOperationException(), found => found);

        Assert.Equal(second, site.Id);
        Assert.Equal("<p>two</p>", site.Content);
        Assert.True(site.IsDraft);
        Assert.Equal(Now.AddMinutes(1), site.CreatedAt);
    }

    [Fact]
    public async Task FindVersion_PublishedVersion_ExposesThePublishDate()
    {
        var about = await SaveAsync("About", "about", true);

        var site = (await QueryAsync(queries => queries.FindVersion(about))).Match(() => throw new InvalidOperationException(), found => found);

        Assert.False(site.IsDraft);
        Assert.Equal(Some(Now), site.PublishedAt);
    }

    [Fact]
    public async Task FindVersion_UnknownVersion_ReturnsNone()
    {
        var about = await SaveAsync("About", "about", true);

        Assert.Equal(None, await QueryAsync(queries => queries.FindVersion(new VersionedKey<Guid>(about.Key, 7))));
    }

    [Fact]
    public async Task FindActiveBySlug_ServesTheActiveVersionNotTheNewestDraft()
    {
        var about = await SaveAsync("About", "about", true, content: "<p>live</p>");
        await SaveAsync("About draft", "about", false, about.Key, 1, "<p>draft</p>");

        var site = (await QueryAsync(queries => queries.FindActiveBySlug("about"))).Match(() => throw new InvalidOperationException(), found => found);

        Assert.Equal(about, site.Id);
        Assert.Equal("<p>live</p>", site.Content);
    }

    [Fact]
    public async Task FindActiveBySlug_DraftOnlySite_ReturnsNone()
    {
        await SaveAsync("About", "about", false);

        Assert.Equal(None, await QueryAsync(queries => queries.FindActiveBySlug("about")));
    }

    [Fact]
    public async Task FindActiveBySlug_UsesTheSlugOfTheActiveVersion()
    {
        var about = await SaveAsync("About", "about", true);
        await SaveAsync("About", "about-us", true, about.Key, 1);

        Assert.Equal(None, await QueryAsync(queries => queries.FindActiveBySlug("about")));
        Assert.NotEqual(None, await QueryAsync(queries => queries.FindActiveBySlug("about-us")));
    }

    [Fact]
    public async Task FindActiveBySlug_UnknownSlug_ReturnsNone()
    {
        Assert.Equal(None, await QueryAsync(queries => queries.FindActiveBySlug("missing")));
    }
}
