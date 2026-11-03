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
