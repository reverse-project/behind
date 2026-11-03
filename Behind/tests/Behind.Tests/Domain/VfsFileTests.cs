namespace Behind.Tests.Domain;

public class VfsFileTests
{
    [Theory]
    [InlineData("logo.png", "logo.png")]
    [InlineData("C:\\temp\\logo.png", "logo.png")]
    [InlineData("/tmp/logo.png", "logo.png")]
    [InlineData("  ", "fallback")]
    [InlineData(null, "fallback")]
    public void ToVfsFileName_KeepsOnlyTheFileName(string? fileName, string expected)
    {
        Assert.Equal(expected, fileName.ToVfsFileName("fallback"));
    }

    [Fact]
    public void ToVfsFileName_TruncatesLongNames()
    {
        var name = new string('a', VfsFile.MaxFileNameLength + 20);

        Assert.Equal(VfsFile.MaxFileNameLength, name.ToVfsFileName("fallback").Length);
    }

    [Fact]
    public void StorageKey_WithoutAParent_IsTheTypeAndTheFileKey()
    {
        var key = Guid.Parse("0a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9");
        var file = new VfsFile(new VersionedKey<Guid>(key, 1), VfsTypes.Sites, "a.png", "image/png", 3, null);

        Assert.Equal("Sites/0a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9", file.StorageKey);
    }

    [Fact]
    public void StorageKey_WithAParent_NestsTheFileUnderTheParent()
    {
        var key = Guid.Parse("0a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9");
        var parent = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var file = new VfsFile(new VersionedKey<Guid>(key, 2), VfsTypes.Galleries, "a.png", "image/png", 3, parent);

        Assert.Equal(
            "Galleries/11111111-2222-3333-4444-555555555555/0a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9",
            file.StorageKey);
    }

    [Fact]
    public void StorageKey_DoesNotChangeWithTheVersion()
    {
        var key = Guid.NewGuid();
        var first = new VfsFile(new VersionedKey<Guid>(key, 1), VfsTypes.Sites, "a.png", "image/png", 3, null);

        Assert.Equal(first.StorageKey, (first with { Id = new VersionedKey<Guid>(key, 2) }).StorageKey);
    }
}
