namespace Behind.Tests.Domain;

public class DownloadTests
{
    [Theory]
    [InlineData("Guide", "guide.pdf", "Guide")]
    [InlineData("  Guide  ", "guide.pdf", "Guide")]
    [InlineData("", "guide.pdf", "guide.pdf")]
    [InlineData("   ", " guide.pdf ", "guide.pdf")]
    [InlineData(null, "guide.pdf", "guide.pdf")]
    public void ToDownloadDisplayName_TrimsAndFallsBackToTheFileName(string? name, string fallback, string expected)
    {
        Assert.Equal(expected, name.ToDownloadDisplayName(fallback));
    }

    [Fact]
    public void ToDownloadDisplayName_ShortensLongNames()
    {
        Assert.Equal(
            Download.MaxDisplayNameLength,
            new string('n', Download.MaxDisplayNameLength + 50).ToDownloadDisplayName("x").Length);
    }

    [Fact]
    public void Constructors_StartWithAnEmptyVersionedKey()
    {
        Assert.Equal(Guid.Empty, new Download("Guide").Id.Key);
        Assert.Equal(Guid.Empty, new DownloadFolder("manuals", "Manuals").Id.Key);
        Assert.Equal(nameof(Download), Download.EntityType);
        Assert.Equal(nameof(DownloadFolder), DownloadFolder.EntityType);
    }

    [Fact]
    public void DownloadFiles_AreTheirOwnKindOfVfsFile()
    {
        Assert.Equal([VfsTypes.Downloads], VfsTypes.SameKindAs(VfsTypes.Downloads));
        Assert.DoesNotContain(VfsTypes.Downloads, VfsTypes.AllSiteImages);
        Assert.DoesNotContain(VfsTypes.Downloads, VfsTypes.GalleryImages);
    }

    [Fact]
    public void StorageKey_NestsTheFileUnderTheDownload()
    {
        var file = Guid.Parse("0a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9");
        var download = Guid.Parse("11111111-2222-3333-4444-555555555555");

        Assert.Equal(
            "Downloads/11111111-2222-3333-4444-555555555555/0a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9",
            VfsTestPaths.Of(VfsTypes.Downloads, file, download));
    }
}
