using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.WebApi.Layouts;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class SiteImageAttachTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<SiteSaveResult> SaveAsync(Guid? siteId, string content, string slug = "about")
    {
        await using var ctx = _bed.Write();
        await using var versions = _bed.WriteVersions();
        var commands = new SiteCommands(
            ctx,
            versions,
            new FixedTimeProvider(NewsTestBed.Now),
            new VfsCommands(ctx, _bed.ContentStore),
            Substitute.For<ICarouselCommands>());

        return siteId is { } id
            ? await commands.SaveVersion(id, "About", slug, content, true)
            : await commands.Create("About", slug, content, true);
    }

    private static string Image(Guid id, string url = VfsTypes.SiteImageUrl) => $"""<img src="{url}/{id}">""";

    private async Task<bool> ExistsAsync(string path) => (await _bed.ContentStore.Read(path)).Match(() => false, _ => true);

    [Fact]
    public async Task LayoutImages_MoveUnderTheConfigurationAndRemainReadable()
    {
        await using var context = _bed.Write();
        var files = new VfsCommands(context, _bed.ContentStore);
        var bytes = new byte[] { 1, 2, 3 };
        var image = await files.Add(new IVfsCommands.Upload(VfsTypes.Layouts, "widget.png", "image/png", bytes));
        await files.AttachSiteImagesOf(VfsTypes.Layouts, LayoutConfiguration.ImagesKey,
            [Image(image, VfsTypes.LayoutImageUrl)]);

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Layouts, image, LayoutConfiguration.ImagesKey)));
        Assert.False(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Layouts, image)));
        var stored = await new Behind.Core.Database.Queries.VfsQueries(context, _bed.ContentStore)
            .Open(VfsTypes.LayoutImages, image);
        Assert.True(stored.Match(() => false, content => content.Bytes.SequenceEqual(bytes)));
    }

    [Fact]
    public async Task Create_MovesTheImagesOfTheContentUnderTheSite()
    {
        var used = await _bed.AddImageAsync();
        var unused = await _bed.AddImageAsync();

        var site = Assert.IsType<SiteSaveResult.Saved>(await SaveAsync(null, Image(used) + Image(used))).Id;

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Sites, used, site.Key)));
        Assert.False(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Sites, used)));
        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Sites, unused)));
    }

    [Fact]
    public async Task SaveVersion_AttachesImagesAddedLaterAndKeepsTheOnesAlreadyAttached()
    {
        var first = await _bed.AddImageAsync();
        var second = await _bed.AddImageAsync();
        var site = Assert.IsType<SiteSaveResult.Saved>(await SaveAsync(null, Image(first))).Id;

        await SaveAsync(site.Key, Image(first) + Image(second));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Sites, first, site.Key)));
        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Sites, second, site.Key)));
        await using var read = _bed.Read();
        Assert.Equal(4, await read.VfsFiles.CountAsync());
    }

    [Fact]
    public async Task Create_ContentWithoutImagesOrWithUnknownImages_AttachesNothing()
    {
        var image = await _bed.AddImageAsync();

        await SaveAsync(null, "<p>text</p>" + Image(Guid.NewGuid()));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Sites, image)));
    }

    [Fact]
    public async Task Create_WithATakenSlug_AttachesNothing()
    {
        await SaveAsync(null, "<p/>", "taken");
        var image = await _bed.AddImageAsync();

        Assert.IsType<SiteSaveResult.SlugTaken>(await SaveAsync(null, Image(image), "taken"));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Sites, image)));
    }

    [Fact]
    public async Task News_MovesTheImagesOfTheSummaryAndTheContentUnderTheNews()
    {
        var inSummary = await _bed.AddImageAsync();
        var inContent = await _bed.AddImageAsync();

        var news = await _bed.SaveNewsAsync(null, summary: Image(inSummary, VfsTypes.NewsImageUrl), content: Image(inContent, VfsTypes.NewsImageUrl));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.News, inSummary, news.Key)));
        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.News, inContent, news.Key)));
    }

    [Fact]
    public async Task Article_MovesTheImagesOfTheSummaryAndTheContentUnderTheArticle()
    {
        var inSummary = await _bed.AddImageAsync();
        var inContent = await _bed.AddImageAsync();

        var article = await _bed.SaveArticleAsync(null, summary: Image(inSummary, VfsTypes.ArticleImageUrl), content: Image(inContent, VfsTypes.ArticleImageUrl));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Articles, inSummary, article.Key)));
        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Articles, inContent, article.Key)));
    }

    [Fact]
    public async Task NewsCategory_MovesTheHeaderImageUnderTheCategory()
    {
        var header = await _bed.AddImageAsync();

        var category = await _bed.CreateCategoryAsync("Releases", Some(header));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.NewsCategories, header, category)));
    }

    [Fact]
    public async Task ArticleCategory_MovesTheHeaderImageUnderTheCategory()
    {
        var header = await _bed.AddImageAsync();

        var category = await _bed.CreateArticleCategoryAsync("Guides", Some(header));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.ArticleCategories, header, category)));
    }

    [Fact]
    public async Task Categories_WithoutAHeaderImage_AttachNothing()
    {
        var image = await _bed.AddImageAsync();

        await _bed.CreateCategoryAsync("Releases");
        await _bed.CreateArticleCategoryAsync("Guides");

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Sites, image)));
    }

    [Fact]
    public async Task News_AlsoAttachesTheImagesThatItStillReferencesWithTheLegacySiteImageUrl()
    {
        var legacy = await _bed.AddImageAsync();

        var news = await _bed.SaveNewsAsync(null, content: Image(legacy));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.News, legacy, news.Key)));
    }

    [Fact]
    public async Task News_IgnoresTheImageUrlsOfOtherContent()
    {
        var image = await _bed.AddVfsFileAsync(VfsTypes.Articles);

        await _bed.SaveNewsAsync(null, content: Image(image, VfsTypes.ArticleImageUrl));

        Assert.True(await ExistsAsync(VfsTestPaths.Of(VfsTypes.Articles, image)));
    }
}
