using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Site = Behind.Core.Domain.Site;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class SiteEndpointsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SiteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static SiteEndpoints.SaveSiteRequest Request(
        string title = "About us",
        string? slug = null,
        string? content = "<p>hi</p>",
        bool publish = false) =>
        new(title, slug, content, publish);

    private static ISiteCommands CommandsReturning(SiteSaveResult result)
    {
        var commands = Substitute.For<ISiteCommands>();
        commands.Create(default!, default!, default!, default, default).ReturnsForAnyArgs(result);
        commands.SaveVersion(default, default!, default!, default!, default, default).ReturnsForAnyArgs(result);
        commands.Activate(default!, default).ReturnsForAnyArgs(result);

        return commands;
    }

    [Fact]
    public async Task List_ReturnsTheSitesWithoutCaching()
    {
        var queries = Substitute.For<ISiteQueries>();
        queries.List(Arg.Any<CancellationToken>()).Returns(
        [
            new ISiteQueries.SiteListing(SiteId, "About", "about", 3, Some(2L), Some("about-old"), Now),
            new ISiteQueries.SiteListing(Guid.Empty, "Draft", "draft", 1, None, None, Now)
        ]);
        var context = new DefaultHttpContext();

        var result = await SiteEndpoints.List(context, queries);

        var body = Assert.IsType<JsonHttpResult<SiteListResponse>>(result).Value!;
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(
            [new SiteListItem(SiteId, "About", "about", 3, 2, "about-old", Now), new SiteListItem(Guid.Empty, "Draft", "draft", 1, null, null, Now)],
            body.Sites);
    }

    [Fact]
    public async Task GetHistory_ReturnsTheVersionsAndTheActiveOne()
    {
        var queries = Substitute.For<ISiteQueries>();
        queries.FindHistory(SiteId, Arg.Any<CancellationToken>()).Returns(
            Some(new ISiteQueries.SiteHistory(
                SiteId,
                Some(1L),
                [
                    new ISiteQueries.SiteVersionInfo(2, "About us", "about", None, Now, false),
                    new ISiteQueries.SiteVersionInfo(1, "About", "about", Some(Now), Now, true)
                ])));

        var result = await SiteEndpoints.GetHistory(SiteId, new DefaultHttpContext(), queries);

        var body = Assert.IsType<JsonHttpResult<SiteHistoryResponse>>(result).Value!;
        Assert.Equal(1, body.ActiveVersion);
        Assert.Equal(
            [
                new SiteVersionItem(2, "About us", "about", null, Now, false, true),
                new SiteVersionItem(1, "About", "about", Now, Now, true, false)
            ],
            body.Versions);
    }

    [Fact]
    public async Task GetHistory_UnknownSite_ReturnsNotFound()
    {
        var queries = Substitute.For<ISiteQueries>();
        queries.FindHistory(SiteId, Arg.Any<CancellationToken>()).Returns(None);

        Assert.IsType<NotFound>(await SiteEndpoints.GetHistory(SiteId, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task GetVersion_ReturnsTheContentOfTheVersion()
    {
        var queries = Substitute.For<ISiteQueries>();
        var key = new VersionedKey<Guid>(SiteId, 2);
        queries.FindVersion(key, Arg.Any<CancellationToken>())
            .Returns(Some(new Site(key, "About", "about", "<p>x</p>", None, Now)));

        var result = await SiteEndpoints.GetVersion(SiteId, 2, new DefaultHttpContext(), queries);

        var body = Assert.IsType<JsonHttpResult<SiteVersionResponse>>(result).Value!;
        Assert.Equal(new SiteVersionResponse(SiteId, 2, "About", "about", "<p>x</p>", null, Now, true), body);
    }

    [Fact]
    public async Task GetVersion_UnknownVersion_ReturnsNotFound()
    {
        var queries = Substitute.For<ISiteQueries>();
        queries.FindVersion(Arg.Any<VersionedKey<Guid>>(), Arg.Any<CancellationToken>()).Returns(None);

        Assert.IsType<NotFound>(await SiteEndpoints.GetVersion(SiteId, 9, new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task Create_NormalizesAndSanitizesTheDraft()
    {
        var commands = CommandsReturning(new SiteSaveResult.Saved(new VersionedKey<Guid>(SiteId, 1)));

        var result = await SiteEndpoints.Create(
            Request("  About us  ", null, "<p>hi</p><script>x</script>", true),
            new DefaultHttpContext(),
            commands);

        var body = Assert.IsType<JsonHttpResult<SiteSavedResponse>>(result).Value!;
        Assert.Equal(new SiteSavedResponse(SiteId, 1), body);
        await commands.Received(1).Create("About us", "about-us", "<p>hi</p>", true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_UsesTheGivenSlugNormalized()
    {
        var commands = CommandsReturning(new SiteSaveResult.Saved(new VersionedKey<Guid>(SiteId, 1)));

        await SiteEndpoints.Create(Request(slug: "My Custom Slug!"), new DefaultHttpContext(), commands);

        await commands.Received(1).Create("About us", "my-custom-slug", "<p>hi</p>", false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_MissingContent_SavesEmptyContent()
    {
        var commands = CommandsReturning(new SiteSaveResult.Saved(new VersionedKey<Guid>(SiteId, 1)));

        await SiteEndpoints.Create(Request(content: null), new DefaultHttpContext(), commands);

        await commands.Received(1).Create("About us", "about-us", string.Empty, false, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("   ", null)]
    [InlineData("!!!", null)]
    [InlineData("About", "***")]
    public async Task Create_WithoutUsableTitleOrSlug_ReturnsBadRequest(string title, string? slug)
    {
        var commands = CommandsReturning(new SiteSaveResult.Saved(new VersionedKey<Guid>(SiteId, 1)));

        var result = await SiteEndpoints.Create(Request(title, slug), new DefaultHttpContext(), commands);

        Assert.IsType<BadRequest>(result);
        await commands.DidNotReceiveWithAnyArgs().Create(default!, default!, default!, default);
    }

    [Fact]
    public async Task Create_SlugTaken_ReturnsConflictWithTheReason()
    {
        var commands = CommandsReturning(new SiteSaveResult.SlugTaken());

        var result = await SiteEndpoints.Create(Request(), new DefaultHttpContext(), commands);

        var conflict = Assert.IsType<Conflict<SiteConflictResponse>>(result);
        Assert.Equal(new SiteConflictResponse("slug-taken"), conflict.Value);
    }

    [Fact]
    public async Task SaveVersion_SavesANewVersionOfTheSite()
    {
        var commands = CommandsReturning(new SiteSaveResult.Saved(new VersionedKey<Guid>(SiteId, 4)));

        var result = await SiteEndpoints.SaveVersion(SiteId, Request(publish: true), new DefaultHttpContext(), commands);

        Assert.Equal(new SiteSavedResponse(SiteId, 4), Assert.IsType<JsonHttpResult<SiteSavedResponse>>(result).Value);
        await commands.Received(1).SaveVersion(SiteId, "About us", "about-us", "<p>hi</p>", true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveVersion_UnknownSite_ReturnsNotFound()
    {
        var commands = CommandsReturning(new SiteSaveResult.NotFound());

        Assert.IsType<NotFound>(await SiteEndpoints.SaveVersion(SiteId, Request(), new DefaultHttpContext(), commands));
    }

    [Fact]
    public async Task SaveVersion_VersionConflict_ReturnsConflictWithTheReason()
    {
        var commands = CommandsReturning(new SiteSaveResult.VersionConflict());

        var result = await SiteEndpoints.SaveVersion(SiteId, Request(), new DefaultHttpContext(), commands);

        Assert.Equal(new SiteConflictResponse("version-conflict"), Assert.IsType<Conflict<SiteConflictResponse>>(result).Value);
    }

    [Fact]
    public async Task Activate_ActivatesTheGivenVersion()
    {
        var commands = CommandsReturning(new SiteSaveResult.Saved(new VersionedKey<Guid>(SiteId, 2)));

        var result = await SiteEndpoints.Activate(SiteId, 2, new DefaultHttpContext(), commands);

        Assert.Equal(new SiteSavedResponse(SiteId, 2), Assert.IsType<JsonHttpResult<SiteSavedResponse>>(result).Value);
        await commands.Received(1).Activate(new VersionedKey<Guid>(SiteId, 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activate_SlugTaken_ReturnsConflict()
    {
        var commands = CommandsReturning(new SiteSaveResult.SlugTaken());

        Assert.IsType<Conflict<SiteConflictResponse>>(await SiteEndpoints.Activate(SiteId, 2, new DefaultHttpContext(), commands));
    }

    [Fact]
    public async Task GetPublic_ReturnsTheActivePublishedSite()
    {
        var queries = Substitute.For<ISiteQueries>();
        queries.FindActiveBySlug("about-us", Arg.Any<CancellationToken>())
            .Returns(Some(new Site(new VersionedKey<Guid>(SiteId, 2), "About us", "about-us", "<p>x</p>", Some(Now), Now)));
        var context = new DefaultHttpContext();

        var result = await SiteEndpoints.GetPublic("About-Us", context, queries);

        Assert.Equal("no-cache", context.Response.Headers.CacheControl);
        Assert.Equal(
            new PublicSiteResponse("About us", "about-us", "<p>x</p>", Now),
            Assert.IsType<JsonHttpResult<PublicSiteResponse>>(result).Value);
    }

    [Fact]
    public async Task GetPublic_UnknownSlug_ReturnsNotFound()
    {
        var queries = Substitute.For<ISiteQueries>();
        queries.FindActiveBySlug(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(None);

        Assert.IsType<NotFound>(await SiteEndpoints.GetPublic("missing", new DefaultHttpContext(), queries));
    }

    [Fact]
    public async Task GetPublic_UnusableSlug_ReturnsNotFoundWithoutQuerying()
    {
        var queries = Substitute.For<ISiteQueries>();

        Assert.IsType<NotFound>(await SiteEndpoints.GetPublic("!!!", new DefaultHttpContext(), queries));
        await queries.DidNotReceiveWithAnyArgs().FindActiveBySlug(default!);
    }

    [Fact]
    public async Task GetPublic_UnpublishedSite_ReturnsNotFound()
    {
        var queries = Substitute.For<ISiteQueries>();
        queries.FindActiveBySlug(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Some(new Site(new VersionedKey<Guid>(SiteId, 1), "Draft", "draft", "<p/>", None, Now)));

        Assert.IsType<NotFound>(await SiteEndpoints.GetPublic("draft", new DefaultHttpContext(), queries));
    }

    [Fact]
    public void GetSettings_ReturnsTheNormalizedIndexSlugAndTheImageLimit()
    {
        var context = new DefaultHttpContext();
        var configuration = Options.Create(new SitesConfiguration { IndexSlug = " Home Page ", MaxImageBytes = 2048 });

        var result = SiteEndpoints.GetSettings(context, configuration);

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(
            new SiteSettingsResponse("home-page", 2048),
            Assert.IsType<JsonHttpResult<SiteSettingsResponse>>(result).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("!!!")]
    public void GetSettings_WithoutAUsableIndexSlug_HasNoIndex(string? indexSlug)
    {
        var result = SiteEndpoints.GetSettings(new DefaultHttpContext(), Options.Create(new SitesConfiguration { IndexSlug = indexSlug }));

        var body = Assert.IsType<JsonHttpResult<SiteSettingsResponse>>(result).Value!;
        Assert.Null(body.IndexSlug);
        Assert.Equal(SitesConfiguration.DefaultMaxImageBytes, body.MaxImageBytes);
    }

    [Fact]
    public async Task GetIndex_ServesTheActiveSiteOfTheConfiguredSlug()
    {
        var queries = Substitute.For<ISiteQueries>();
        queries.FindActiveBySlug("home", Arg.Any<CancellationToken>())
            .Returns(Some(new Site(new VersionedKey<Guid>(SiteId, 2), "Home", "home", "<p>x</p>", Some(Now), Now)));
        var context = new DefaultHttpContext();

        var result = await SiteEndpoints.GetIndex(context, queries, Options.Create(new SitesConfiguration { IndexSlug = "Home" }));

        Assert.Equal("no-cache", context.Response.Headers.CacheControl);
        Assert.Equal(
            new PublicSiteResponse("Home", "home", "<p>x</p>", Now),
            Assert.IsType<JsonHttpResult<PublicSiteResponse>>(result).Value);
    }

    [Fact]
    public async Task GetIndex_WithoutAConfiguredSlug_ReturnsNotFoundWithoutQuerying()
    {
        var queries = Substitute.For<ISiteQueries>();

        var result = await SiteEndpoints.GetIndex(new DefaultHttpContext(), queries, Options.Create(new SitesConfiguration()));

        Assert.IsType<NotFound>(result);
        await queries.DidNotReceiveWithAnyArgs().FindActiveBySlug(default!);
    }

    [Fact]
    public async Task GetIndex_ConfiguredSlugWithoutALiveSite_ReturnsNotFound()
    {
        var queries = Substitute.For<ISiteQueries>();
        queries.FindActiveBySlug(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(None);

        var result = await SiteEndpoints.GetIndex(new DefaultHttpContext(), queries, Options.Create(new SitesConfiguration { IndexSlug = "home" }));

        Assert.IsType<NotFound>(result);
    }
}
