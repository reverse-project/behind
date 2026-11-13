using System.Security.Claims;
using System.Text.Json;
using Behind.Common.Database;
using Behind.Common.Database.Raw;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Infrastructure.Localization;
using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Commands;
using Behind.Core.WebApi;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Layouts;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public sealed class ContentLocaleTests : IDisposable
{
    private readonly TestDbContextFactory factory = new();
    private static LocaleCatalog Locales() =>
        new(new ManifestEmbeddedFileProvider(typeof(CoreModule).Assembly, "Frontend"));
    private static IAuthorizationService Authorization(bool allowed = false)
    {
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        return authorization;
    }

    public void Dispose() => factory.Dispose();

    private async Task<(Guid Neutral, Guid Hungarian, Guid English, Guid Child, Guid File)> Seed()
    {
        await using var context = factory.CreateContext();
        var neutral = Add("Neutral");
        var hungarian = Add("Hungarian");
        var english = Add("English");
        var child = new SiteSection(SiteSectionTheme.Normal, "Section", "Localized body", "", SiteSectionImagePosition.Left);
        context.SiteSections.Add(child);
        var childId = Guid.NewGuid();
        context.Entry(child).StampVersionedKey(childId, 1);
        context.Entry(child).Property<Guid>(SiteSectionConfiguration.SiteIdProperty).CurrentValue = hungarian;
        var file = new VfsFile(VfsTypes.Sites, "image.png", "image/png", 1, hungarian);
        context.VfsFiles.Add(file);
        var fileId = Guid.NewGuid();
        context.Entry(file).StampVersionedKey(fileId, 1);
        await context.StageContentLocale(nameof(Site), hungarian, "hu-HU");
        await context.StageContentLocale(nameof(Site), english, "en-US");
        await context.SaveChangesAsync();
        return (neutral, hungarian, english, childId, fileId);

        Guid Add(string title)
        {
            var site = new Site(title, title.ToLowerInvariant(), "", DateTimeOffset.UtcNow);
            context.Sites.Add(site);
            var id = Guid.NewGuid();
            context.Entry(site).Property<Guid>(SiteConfiguration.SiteIdProperty).CurrentValue = id;
            context.Entry(site).Property<long>(SiteConfiguration.VersionProperty).CurrentValue = 1;
            return id;
        }
    }

    private static async Task Invoke(
        AbstractCoreContext context, HttpContext http, bool authorized = false, ILayoutService? layouts = null)
    {
        var middleware = new ContentLocaleMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(http, context, Locales(), Authorization(authorized), layouts);
    }

    [Theory]
    [InlineData(null, null, 0)]
    [InlineData("hu-HU", "Hungarian", 1)]
    [InlineData("HU-hu", "Hungarian", 1)]
    [InlineData("en-US", "English", 0)]
    public async Task PublicListsIncludeUnlocalizedContentAndKeepOwnedContentLocaleScoped(
        string? locale, string? localizedTitle, int owned)
    {
        await Seed();
        await using var context = factory.CreateContext();
        var http = new DefaultHttpContext();
        if (locale is not null)
        {
            http.Request.Headers[ContentLocaleMiddleware.Header] = locale;
        }
        http.Request.Headers["X-Content-All-Locales"] = "true";
        await Invoke(context, http, true);
        var visibleTitles = await context.Sites.Select(site => site.Title).ToArrayAsync();
        Assert.Contains("Neutral", visibleTitles);
        Assert.Equal(locale is null ? 1 : 2, visibleTitles.Length);
        if (localizedTitle is not null)
        {
            Assert.Contains(localizedTitle, visibleTitles);
        }
        Assert.Equal(owned, await context.SiteSections.CountAsync());
        Assert.Equal(owned, await context.VfsFiles.CountAsync());
        Assert.False(context.IncludeAllContentLocales);
        Assert.Equal("private, no-store", http.Response.Headers.CacheControl);
        Assert.Contains(ContentLocaleMiddleware.Header, http.Response.Headers.Vary.ToString());
    }

    [Fact]
    public async Task DirectLocaleQueriesIncludeUnassignedContentAsSharedContent()
    {
        await Seed();
        await using var context = factory.CreateContext();
        var localized = await context.InContentLocale(context.Sites, nameof(Site), "hu-HU")
            .Select(site => site.Title).ToArrayAsync();
        var unlocalized = await context.InContentLocale(context.Sites, nameof(Site), null)
            .Select(site => site.Title).ToArrayAsync();

        Assert.Contains("Neutral", localized);
        Assert.Contains("Hungarian", localized);
        Assert.DoesNotContain("English", localized);
        Assert.Equal(["Neutral"], unlocalized);
    }

    [Fact]
    public async Task ExplicitlyLocalizedChildStillRequiresVisibleParent()
    {
        var ids = await Seed();
        await using var context = factory.CreateContext();
        await context.StageContentLocale(nameof(SiteSection), ids.Child, "en-US");
        await context.SaveChangesAsync();
        var http = new DefaultHttpContext();
        http.Request.Headers[ContentLocaleMiddleware.Header] = "en-US";
        await Invoke(context, http);
        Assert.Empty(await context.SiteSections.ToListAsync());
    }

    [Fact]
    public async Task EqualIdsOfDifferentEntityTypesNeverOverwriteEachOthersLocale()
    {
        var ids = await Seed();
        await using var context = factory.CreateContext();
        var carousel = new Carousel(5);
        context.Carousels.Add(carousel);
        context.Entry(carousel).StampVersionedKey(ids.Hungarian, 1);
        await context.StageContentLocale(nameof(Carousel), ids.Hungarian, "en-US");
        await context.SaveChangesAsync();
        var http = new DefaultHttpContext();
        http.Request.Headers[ContentLocaleMiddleware.Header] = "hu-HU";
        await Invoke(context, http);
        Assert.Contains(await context.Sites.Select(site => site.Title).ToArrayAsync(), title => title == "Hungarian");
        Assert.Empty(await context.Carousels.ToListAsync());
    }

    [Fact]
    public async Task ClearingChildLocaleRestoresInheritance()
    {
        var ids = await Seed();
        await using var context = factory.CreateContext();
        await context.StageContentLocale(nameof(SiteSection), ids.Child, "en-US");
        await context.SaveChangesAsync();
        await context.StageContentLocale(nameof(SiteSection), ids.Child, null);
        await context.SaveChangesAsync();
        var http = new DefaultHttpContext();
        http.Request.Headers[ContentLocaleMiddleware.Header] = "hu-HU";
        await Invoke(context, http);
        Assert.Single(await context.SiteSections.ToListAsync());
    }

    [Theory]
    [InlineData("/sites", "Site.Read", true, true)]
    [InlineData("/sites", "Site.Read", false, false)]
    [InlineData("/news/{id:guid}", "News.Write", true, false)]
    [InlineData("/news/manage", "News.Read", true, true)]
    public async Task OnlyActualAuthorizedManagementEndpointsCanSeeEveryLocale(
        string route, string policy, bool authorized, bool all)
    {
        await Seed();
        await using var context = factory.CreateContext();
        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "editor")], "test"));
        http.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(route), 0,
            new EndpointMetadataCollection(new AuthorizeAttribute(policy)), "test"));
        await Invoke(context, http, authorized);
        Assert.Equal(all, context.IncludeAllContentLocales);
        Assert.Equal(all ? 3 : 1, await context.Sites.CountAsync());
    }

    [Theory]
    [InlineData("/content-restrictions/{entityType}/{id:guid}")]
    [InlineData("/content-restrictions")]
    public async Task RestrictionAssignmentEndpointsResolveEntityPermissionsAcrossLocales(string route)
    {
        await Seed();
        await using var context = factory.CreateContext();
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "editor")], "test"));
        http.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(route), 0,
            new EndpointMetadataCollection(new AuthorizeAttribute()), "test"));
        await Invoke(context, http);
        Assert.True(context.IncludeAllContentLocales);
        Assert.Equal(3, await context.Sites.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("xx-XX")]
    [InlineData("*")]
    public async Task InvalidLocaleCannotFallBackToNeutral(string locale)
    {
        await using var context = factory.CreateContext();
        var http = new DefaultHttpContext();
        http.Request.Headers[ContentLocaleMiddleware.Header] = locale;
        await Invoke(context, http);
        Assert.Equal(400, http.Response.StatusCode);
    }

    [Fact]
    public async Task AssignmentRequiresExistingEntityEditPolicyAndVersionsAreAppendOnly()
    {
        var ids = await Seed();
        await using var context = factory.CreateContext();
        var http = new DefaultHttpContext();
        var request = new ContentLocaleEndpoints.SaveRequest(nameof(Site), ids.Neutral, "hu-HU");
        var forbidden = await ContentLocaleEndpoints.Save(request, http, context, Locales(), Authorization(), []);
        Assert.Equal(403, Assert.IsAssignableFrom<IStatusCodeHttpResult>(forbidden).StatusCode);
        Assert.IsType<NoContent>(await ContentLocaleEndpoints.Save(request, http, context, Locales(), Authorization(true), []));
        Assert.IsType<NoContent>(await ContentLocaleEndpoints.Save(request with { Locale = null },
            http, context, Locales(), Authorization(true), []));
        Assert.Equal(2, await context.ContentLocales.CountAsync(item => item.EntityId == ids.Neutral));
        Assert.Null(await context.FindContentLocale(nameof(Site), ids.Neutral));
    }

    [Fact]
    public async Task AssignmentReadIncludesEffectiveInheritedLocaleForEditorPreviews()
    {
        var ids = await Seed();
        await using var context = factory.CreateContext();
        var result = await ContentLocaleEndpoints.Get(nameof(SiteSection), ids.Child,
            new DefaultHttpContext(), context, Authorization(true), []);
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        var json = JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(JsonValueKind.Null, json.GetProperty("locale").ValueKind);
        Assert.Equal("hu-HU", json.GetProperty("effectiveLocale").GetString());
    }

    [Fact]
    public async Task BatchedAssignmentReadsAuthorizeEachEntityAndReturnInheritedScopes()
    {
        var ids = await Seed();
        await using var context = factory.CreateContext();
        var request = new ContentLocaleEndpoints.QueryRequest(
            [new(nameof(Site), ids.Hungarian), new(nameof(SiteSection), ids.Child), new(nameof(Site), Guid.NewGuid())]);
        var result = await ContentLocaleEndpoints.Query(request, new DefaultHttpContext(), context, Authorization(true), []);
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        var json = JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var items = json.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal("hu-HU", items[0].GetProperty("locale").GetString());
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("locale").ValueKind);
        Assert.Equal("hu-HU", items[1].GetProperty("effectiveLocale").GetString());
        Assert.Equal(404, items[2].GetProperty("status").GetInt32());
        var denied = await ContentLocaleEndpoints.Query(request, new DefaultHttpContext(), context, Authorization(), []);
        var forbidden = JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<IValueHttpResult>(denied).Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("items").EnumerateArray();
        Assert.All(forbidden, item =>
        {
            Assert.Equal(403, item.GetProperty("status").GetInt32());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("effectiveLocale").ValueKind);
        });
    }

    [Fact]
    public async Task SameAliasInDifferentLocalesIsSavedAtomicallyAndSameLocaleConflicts()
    {
        await using var context = factory.CreateContext();
        var commands = new RouteAliasCommands(context);
        context.ContentLocaleSpecified = true;
        context.SelectedContentLocale = "hu-HU";
        var hungarian = Assert.IsType<RouteAliasSaveResult.Saved>(
            await commands.Create(new("shared", "#/news")));
        context.SelectedContentLocale = "en-US";
        var english = Assert.IsType<RouteAliasSaveResult.Saved>(
            await commands.Create(new("shared", "#/articles")));
        Assert.IsType<RouteAliasSaveResult.AliasTaken>(await commands.Create(new("shared", "#/galleries")));
        Assert.Equal("hu-HU", await context.FindContentLocale(nameof(RouteAlias), hungarian.Id.Key));
        Assert.Equal("en-US", await context.FindContentLocale(nameof(RouteAlias), english.Id.Key));
    }

    [Fact]
    public async Task SameSiteSlugPerLocaleAndStableChildAssignmentsSurviveVersioning()
    {
        await using var context = factory.CreateContext();
        await using var versions = factory.CreateVersioningContext();
        var commands = new SiteCommands(context, versions, TimeProvider.System,
            Substitute.For<IVfsCommands>(), Substitute.For<ICarouselCommands>());
        context.ContentLocaleSpecified = true;
        context.SelectedContentLocale = "hu-HU";
        var draft = new ISiteCommands.SiteSectionDraft(SiteSectionTheme.Normal, "Child", "Body", None, "",
            SiteSectionImagePosition.Left);
        var first = Assert.IsType<SiteSaveResult.Saved>(await commands.Create("Hungarian", "shared", "", false, false, [draft]));
        var section = await context.SiteSections.SingleAsync();
        var child = context.Entry(section).Property<Guid>(VersionedColumns.Key).CurrentValue;
        await context.StageContentLocale(nameof(SiteSection), child, "hu-HU");
        await context.SaveChangesAsync();
        Assert.IsType<SiteSaveResult.Saved>(await commands.SaveVersion(first.Id.Key, "Updated", "shared", "", false,
            false, [draft with { Id = child }]));
        Assert.Equal(2, await context.SiteSections.CountAsync(item => EF.Property<Guid>(item, VersionedColumns.Key) == child));
        Assert.Equal("hu-HU", await context.FindContentLocale(nameof(SiteSection), child));
        context.SelectedContentLocale = "en-US";
        Assert.IsType<SiteSaveResult.Saved>(await commands.Create("English", "shared", "", false));
        Assert.IsType<SiteSaveResult.SlugTaken>(await commands.Create("Conflict", "shared", "", false));
        Assert.IsType<SiteSaveResult.UnknownImage>(await commands.Create("Foreign child", "foreign", "", false,
            false, [draft with { Id = child }]));
    }

    [Fact]
    public async Task RootDownloadFolderSlugIsUniqueOnlyWithinItsLocale()
    {
        await using var context = factory.CreateContext();
        var commands = new DownloadFolderCommands(context, Substitute.For<IVfsCommands>());
        context.ContentLocaleSpecified = true;
        context.SelectedContentLocale = "hu-HU";
        Assert.IsType<DownloadFolderSaveResult.Saved>(await commands.Create(None, new("shared", "Hungarian")));
        context.SelectedContentLocale = "en-US";
        Assert.IsType<DownloadFolderSaveResult.Saved>(await commands.Create(None, new("shared", "English")));
        Assert.IsType<DownloadFolderSaveResult.SlugTaken>(await commands.Create(None, new("shared", "Duplicate")));
    }

    [Fact]
    public async Task MetadataSoftDeleteRestoreAndNewVersionPreserveHistory()
    {
        await using (var seed = factory.CreateContext())
        {
            await seed.EnsureBuiltInUsersAsync();
        }
        await using var context = factory.CreateAuditedContext(Behind.Core.Database.Auditing.AuditActor.Autonomous);
        var contentId = Guid.NewGuid();
        await context.StageContentLocale(nameof(Site), contentId, "hu-HU");
        await context.SaveChangesAsync();
        var key = await context.ContentLocales.Select(item => EF.Property<Guid>(item, VersionedColumns.Key)).SingleAsync();
        await context.MarkSoftDeletedAsync(context.ContentLocales.OfKey(key));
        await context.SaveChangesAsync();
        Assert.Empty(await context.ContentLocales.ToListAsync());
        await context.StageContentLocale(nameof(Site), contentId, "hu-HU");
        await context.SaveChangesAsync();
        Assert.Equal("hu-HU", await context.FindContentLocale(nameof(Site), contentId));
        using var scope = context.SoftDelete.IncludeDeleted();
        Assert.Equal(2, await context.ContentLocales.CountAsync());
        await context.MarkRestoredAsync(context.ContentLocales.OfKey(key));
        await context.SaveChangesAsync();
        Assert.Equal(2, await context.ContentLocales.CountAsync());
    }

    [Fact]
    public async Task MetadataHasRawEditorAndHumanReadableAuditLabels()
    {
        await using var context = factory.CreateContext();
        var id = Guid.NewGuid();
        var assignment = new ContentLocale(new(id, 1), nameof(Site), Guid.NewGuid(), "hu-HU");
        context.ContentLocales.Add(assignment);
        context.Entry(assignment).StampVersionedKey(id, 1);
        await context.SaveChangesAsync();
        Assert.Contains(context.RawTables, table => table.Name == "ContentLocales");
        var labels = await context.ResolveAuditLabelsAsync([id], []);
        Assert.Equal("Site locale hu-HU", labels.Label(id.ToString()));
    }

    [Fact]
    public void SiteAndAliasDraftsDistinguishOmittedLocaleFromExplicitNeutral()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var omitted = JsonSerializer.Deserialize<RouteAliasEndpoints.SaveRouteAliasRequest>(
            """{"alias":"hello","target":"#/news"}""", options)!;
        var neutral = JsonSerializer.Deserialize<RouteAliasEndpoints.SaveRouteAliasRequest>(
            """{"alias":"hello","target":"#/news","contentLocale":null}""", options)!;
        Assert.False(omitted.ContentLocaleSpecified);
        Assert.True(neutral.ContentLocaleSpecified);
        Assert.Null(neutral.ContentLocale);
    }

    [Fact]
    public async Task LayoutsIncludeSharedAndSelectedLocaleDefaultsAssignmentsAndContent()
    {
        var layouts = Substitute.For<ILayoutService>();
        layouts.Read(Arg.Any<CancellationToken>()).Returns(new LayoutConfiguration
        {
            Layouts =
            [
                new(),
                new() { Id = "hungarian", Locale = "hu-HU" },
                new() { Id = "english", Locale = "en-US" }
            ],
            Assignments = [new("#/news", "default"), new("#/news", "hungarian"), new("#/articles", "english")]
        });
        var http = new DefaultHttpContext();
        http.Items[ContentLocaleMiddleware.Header] = "hu-HU";
        var result = await LayoutEndpoints.Get(layouts, CancellationToken.None, http);
        var value = Assert.IsType<LayoutConfiguration>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("default", value.DefaultLayout);
        Assert.Equal(["default", "hungarian"], value.Layouts.Select(layout => layout.Id));
        Assert.Equal(["#/news", "#/news"], value.Assignments.Select(assignment => assignment.Route));
        Assert.Contains(CoreModule.Descriptor.Widgets, widget => widget.Key == "core.locale-switch");
        Assert.True(ModuleCatalog.Create([CoreModule.Descriptor]).Match(_ => false, _ => true));
    }
}
