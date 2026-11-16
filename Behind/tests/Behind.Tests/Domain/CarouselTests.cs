namespace Behind.Tests.Domain;

public class CarouselTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#/about")]
    [InlineData("/sites/slug/about")]
    [InlineData("https://example.com/path?x=1")]
    [InlineData("http://example.com")]
    [InlineData("mailto:team@example.com")]
    public void IsSafeCarouselLink_AcceptsLocalAndWebLinks(string? link)
    {
        Assert.True(link.IsSafeCarouselLink);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html;base64,AAAA")]
    [InlineData("vbscript:x")]
    [InlineData("//evil.example.com")]
    [InlineData("/\\evil.example.com")]
    [InlineData(" https://example.com")]
    [InlineData("example.com")]
    [InlineData("about")]
    public void IsSafeCarouselLink_RejectsEverythingElse(string link)
    {
        Assert.False(link.IsSafeCarouselLink);
    }

    [Fact]
    public void IsSafeCarouselLink_RejectsTooLongLinks()
    {
        Assert.False(("https://example.com/" + new string('a', CarouselSlide.MaxLinkUrlLength)).IsSafeCarouselLink);
    }

    [Fact]
    public void VfsTypes_CarouselsHaveTheirOwnKindAndUrl()
    {
        Assert.Equal(VfsTypes.CarouselImages, VfsTypes.SameKindAs(VfsTypes.Carousels));
        Assert.Equal(VfsTypes.CarouselImageUrl, VfsTypes.ImageUrlOf(VfsTypes.Carousels));
        Assert.DoesNotContain(VfsTypes.Carousels, VfsTypes.AllSiteImages);
        Assert.Equal("/carousels/images", VfsTypes.CarouselImageUrl);
    }

    [Fact]
    public void Constructors_StartWithAnEmptyVersionedKey()
    {
        Assert.Equal(Guid.Empty, new Carousel(5).Id.Key);
        Assert.Equal(Guid.Empty, new CarouselSlide("caption", "").Id.Key);
    }
}
