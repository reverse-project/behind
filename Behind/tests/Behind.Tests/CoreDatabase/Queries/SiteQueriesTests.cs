using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Tests.Helpers;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class SiteQueriesTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TestDbContextFactory _factory = new();

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
        var commands = new SiteCommands(ctx, versions, new FixedTimeProvider(Now.AddMinutes(minutes)));
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

    [Fact]
    public async Task List_ReturnsTheLatestVersionOfEverySiteNewestFirst()
    {
        var about = await SaveAsync("About", "about", true);
        await SaveAsync("About us", "about-us", false, about.Key, 1);
        await SaveAsync("Contact", "contact", false, minutes: 5);

        var sites = await QueryAsync(queries => queries.List());

        Assert.Equal(["Contact", "About us"], sites.Select(site => site.Title));
        Assert.Equal(["contact", "about-us"], sites.Select(site => site.Slug));
        var listed = sites[1];
        Assert.Equal(about.Key, listed.Id);
        Assert.Equal(2L, listed.LatestVersion);
        Assert.Equal(Some(1L), listed.ActiveVersion);
        Assert.Equal(Some("about"), listed.ActiveSlug);
        Assert.Equal(Now.AddMinutes(1), listed.UpdatedAt);
        Assert.Equal(None, sites[0].ActiveVersion);
        Assert.Equal(None, sites[0].ActiveSlug);
    }

    [Fact]
    public async Task List_WithoutSites_IsEmpty()
    {
        Assert.Empty(await QueryAsync(queries => queries.List()));
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
