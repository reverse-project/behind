using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.Common.Configuration;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi;

public sealed class ContentItemBrowserTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Browser_SupportsEveryRegisteredCoreContentType()
    {
        await using var context = _factory.CreateContext();
        foreach (var type in context.RestrictedContentTypes)
        {
            var page = await context.BrowseContentItems(type, new Flipper(1, 20), null);
            Assert.Empty(page.Items);
        }
    }

    [Fact]
    public async Task RestrictionList_ResolvesItemLabelsAndFallsBackForMissingItems()
    {
        await using var context = _factory.CreateContext();
        var id = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var category = new NewsCategory("Readable");
        context.NewsCategories.Add(category);
        context.Entry(category).StampVersionedKey(id, 1);
        AddRule(id);
        AddRule(missing);
        await context.SaveChangesAsync();
        var options = Substitute.For<IOptionsMonitor<ContentRestrictionsConfiguration>>();
        options.CurrentValue.Returns(new ContentRestrictionsConfiguration());
        var result = Assert.IsAssignableFrom<IValueHttpResult>(
            await ContentRestrictionEndpoints.List(new DefaultHttpContext(), context, options, []));
        var json = JsonSerializer.SerializeToElement(result.Value);
        var items = json.GetProperty("Items").EnumerateArray().ToArray();
        Assert.Contains(items, item => item.GetProperty("DisplayName").GetString() == "Readable");
        Assert.Contains(items, item => item.GetProperty("DisplayName").GetString() == missing.ToString());

        void AddRule(Guid target)
        {
            var rule = new ContentRestriction(nameof(NewsCategory), target, 18);
            context.ContentRestrictions.Add(rule);
            context.Entry(rule).StampVersionedKey(Guid.NewGuid(), 1);
        }
    }

    [Fact]
    public async Task Browser_PreservesDeletionAndAgeVisibilityFilters()
    {
        await using var context = _factory.CreateContext();
        var id = Guid.NewGuid();
        var category = new NewsCategory("Hidden");
        context.NewsCategories.Add(category);
        context.Entry(category).StampVersionedKey(id, 1);
        await context.SaveChangesAsync();
        context.HiddenContentIds = [id];
        Assert.Empty((await context.BrowseContentItems(typeof(NewsCategory), new Flipper(1, 20), null)).Items);
        context.HiddenContentIds = [];
        await context.MarkSoftDeletedAsync(context.NewsCategories.OfKey(id));
        await context.SaveChangesAsync();
        Assert.Empty((await context.BrowseContentItems(typeof(NewsCategory), new Flipper(1, 20), null)).Items);
    }

    [Fact]
    public async Task Endpoint_ReturnsNamedItemsAndNoStore()
    {
        await using var context = _factory.CreateContext();
        var category = new NewsCategory("Browsable");
        context.NewsCategories.Add(category);
        context.Entry(category).StampVersionedKey(Guid.NewGuid(), 1);
        await context.SaveChangesAsync();
        var http = new DefaultHttpContext();
        var result = Assert.IsType<JsonHttpResult<ContentItemPage>>(
            await ContentRestrictionEndpoints.Browse(nameof(NewsCategory), http, context));
        Assert.Equal("Browsable", Assert.Single(result.Value!.Items).DisplayName);
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Browser_UsesLatestTitlesSearchAndPaging()
    {
        await using var context = _factory.CreateContext();
        var id = Guid.NewGuid();
        Add(id, 1, "Old title");
        Add(id, 2, "New title");
        Add(Guid.NewGuid(), 1, "Other title");
        await context.SaveChangesAsync();

        var page = await context.BrowseContentItems(typeof(NewsCategory), new Flipper(1, 1), "title");
        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.PageCount);
        Assert.Equal("New title", Assert.Single(page.Items).DisplayName);
        var found = await context.BrowseContentItems(typeof(NewsCategory), new Flipper(1, 20), id.ToString());
        Assert.Equal(id, Assert.Single(found.Items).Id);
        Assert.Empty((await context.BrowseContentItems(typeof(NewsCategory), new Flipper(1, 20), "Old")).Items);

        void Add(Guid key, long version, string name)
        {
            var category = new NewsCategory(name);
            context.NewsCategories.Add(category);
            context.Entry(category).StampVersionedKey(key, version);
        }
    }

    [Fact]
    public async Task Browser_UsesFileNamesAndFallsBackToId()
    {
        await using var context = _factory.CreateContext();
        var file = new VfsFile(VfsTypes.News, "photo.png", "image/png", 1);
        var blank = new VfsFile(VfsTypes.News, " ", "image/png", 1);
        context.VfsFiles.Add(file);
        var fileId = Guid.NewGuid();
        var blankId = Guid.NewGuid();
        context.Entry(file).StampVersionedKey(fileId, 1);
        context.VfsFiles.Add(blank);
        context.Entry(blank).StampVersionedKey(blankId, 1);
        await context.SaveChangesAsync();

        var page = await context.BrowseContentItems(typeof(VfsFile), new Flipper(1, 20), null);
        Assert.Equal("photo.png", page.Items.Single(item => item.Id == fileId).DisplayName);
        Assert.Equal(blankId.ToString(), page.Items.Single(item => item.Id == blankId).DisplayName);
    }

    [Fact]
    public async Task Endpoint_RejectsUnsupportedTypesAndOverlongSearch()
    {
        await using var context = _factory.CreateContext();
        Assert.IsType<BadRequest>(await ContentRestrictionEndpoints.Browse(
            "HumanUser", new DefaultHttpContext(), context));
        Assert.IsType<BadRequest>(await ContentRestrictionEndpoints.Browse(
            nameof(News), new DefaultHttpContext(), context, search: new string('x', 201)));
    }
}
