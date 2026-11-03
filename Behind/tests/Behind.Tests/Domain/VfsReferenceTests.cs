namespace Behind.Tests.Domain;

public class VfsReferenceTests
{
    private static readonly Guid First = Guid.Parse("01a1978f-6782-7486-b127-5e72285ad363");
    private static readonly Guid Second = Guid.Parse("01a19df7-74c7-77c2-81e6-2e2ed9dacdfb");

    [Fact]
    public void ReferencedKeys_FindsEveryDistinctKeyUnderTheUrl()
    {
        var content = $"""
                       <p><img src="/sites/images/{First}"><img src="/sites/images/{Second}"></p>
                       <a href="/sites/images/{First.ToString().ToUpperInvariant()}">again</a>
                       """;

        Assert.Equal([First, Second], content.ReferencedKeys(VfsTypes.SiteImageUrl));
    }

    [Fact]
    public void ReferencedKeys_IgnoresOtherUrlsAndMalformedKeys()
    {
        var content = $"""<img src="/galleries/images/{First}"><img src="/sites/images/not-a-guid"><img src="/sites/images/1234">""";

        Assert.Empty(content.ReferencedKeys(VfsTypes.SiteImageUrl));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ReferencedKeys_NoContent_ReturnsNothing(string? content)
    {
        Assert.Empty(content.ReferencedKeys(VfsTypes.SiteImageUrl));
    }
}

public class VfsTypesTests
{
    [Theory]
    [InlineData(VfsTypes.Sites, VfsTypes.SiteImageUrl)]
    [InlineData(VfsTypes.News, VfsTypes.NewsImageUrl)]
    [InlineData(VfsTypes.NewsCategories, VfsTypes.NewsImageUrl)]
    [InlineData(VfsTypes.Articles, VfsTypes.ArticleImageUrl)]
    [InlineData(VfsTypes.ArticleCategories, VfsTypes.ArticleImageUrl)]
    public void ImageUrlOf_IsTheImageUrlOfTheContentOfTheType(string type, string expected)
    {
        Assert.Equal(expected, VfsTypes.ImageUrlOf(type));
    }

    [Fact]
    public void ReferencedImageUrlsOf_NewsAndArticles_AlsoAcceptTheLegacySiteImageUrl()
    {
        Assert.Equal([VfsTypes.SiteImageUrl], VfsTypes.ReferencedImageUrlsOf(VfsTypes.Sites));
        Assert.Equal([VfsTypes.NewsImageUrl, VfsTypes.SiteImageUrl], VfsTypes.ReferencedImageUrlsOf(VfsTypes.News));
        Assert.Equal([VfsTypes.ArticleImageUrl, VfsTypes.SiteImageUrl], VfsTypes.ReferencedImageUrlsOf(VfsTypes.Articles));
    }

    [Fact]
    public void SameKindAs_KeepsTheGalleryImagesApartFromTheSiteImages()
    {
        Assert.Equal([VfsTypes.Galleries], VfsTypes.SameKindAs(VfsTypes.Galleries));
        Assert.DoesNotContain(VfsTypes.Galleries, VfsTypes.SameKindAs(VfsTypes.News));
        Assert.Contains(VfsTypes.ArticleCategories, VfsTypes.SameKindAs(VfsTypes.Sites));
    }
}
