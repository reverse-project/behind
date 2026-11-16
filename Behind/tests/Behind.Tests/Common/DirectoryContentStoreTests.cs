using Behind.Common.Storage;

namespace Behind.Tests.Common;

public sealed class DirectoryContentStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"behind-store-{Guid.NewGuid():N}");

    private DirectoryContentStore Store => new(_root);

    private static byte[]? Bytes(Option<byte[]> content) => content.Match<byte[]?>(() => null, bytes => bytes);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public async Task Write_ThenRead_RoundTripsTheContentInNestedKeys()
    {
        byte[] content = [1, 2, 3];

        await Store.Write("ab/file-1", content);

        Assert.Equal(content, Bytes(await Store.Read("ab/file-1")));
        Assert.True(File.Exists(Path.Combine(_root, "ab", "file-1")));
    }

    [Fact]
    public async Task Write_ReplacesExistingContentAndLeavesNoTemporaryFiles()
    {
        await Store.Write("a/file", [1]);
        await Store.Write("a/file", [2, 2]);

        Assert.Equal(new byte[] { 2, 2 }, Bytes(await Store.Read("a/file")));
        Assert.Single(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Read_MissingKey_ReturnsNone()
    {
        Assert.Equal(None, await Store.Read("missing/file"));

        await Store.Write("here/file", [1]);

        Assert.Equal(None, await Store.Read("here/other"));
    }

    [Fact]
    public async Task Delete_RemovesTheContentAndIgnoresMissingKeys()
    {
        await Store.Write("a/file", [1]);

        await Store.Delete("a/file");
        await Store.Delete("a/file");
        await Store.Delete("never/existed");

        Assert.Equal(None, await Store.Read("a/file"));
    }

    [Fact]
    public async Task Move_RelocatesTheContentAndCreatesTheTargetFolders()
    {
        await Store.Write("Sites/file", [1, 2]);

        await Store.Move("Sites/file", "Sites/parent/file");

        Assert.Equal(None, await Store.Read("Sites/file"));
        Assert.Equal(new byte[] { 1, 2 }, Bytes(await Store.Read("Sites/parent/file")));
    }

    [Fact]
    public async Task Move_MissingSource_DoesNothing()
    {
        await Store.Move("missing", "somewhere/else");

        Assert.Equal(None, await Store.Read("somewhere/else"));
    }

    [Fact]
    public async Task Move_OutsideTheRoot_IsRejected()
    {
        await Store.Write("a/file", [1]);

        await Assert.ThrowsAsync<ArgumentException>(() => Store.Move("a/file", "../escape"));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.Move("../escape", "a/other"));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/../../escape")]
    [InlineData("/etc/passwd")]
    public async Task Keys_OutsideTheRoot_AreRejected(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Store.Write(key, [1]));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.Read(key));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.Delete(key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task EmptyKeys_AreRejected(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Store.Write(key, [1]));
    }

    [Fact]
    public async Task Write_NullContent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Store.Write("a/file", null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_EmptyRoot_Throws(string root)
    {
        Assert.Throws<ArgumentException>(() => new DirectoryContentStore(root));
    }

    [Fact]
    public async Task Stores_WithTheSameRoot_ShareTheirContent()
    {
        await new DirectoryContentStore(_root).Write("a/file", [9]);

        Assert.Equal(new byte[] { 9 }, Bytes(await new DirectoryContentStore(_root + "/").Read("a/file")));
    }
}
