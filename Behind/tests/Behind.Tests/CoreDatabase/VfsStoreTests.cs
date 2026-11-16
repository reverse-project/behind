using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class VfsStoreTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static IVfsQueries.Content Found(Option<IVfsQueries.Content> option) =>
        option.Match<IVfsQueries.Content>(() => throw new InvalidOperationException(), content => content);

    private static byte[]? Bytes(Option<byte[]> content) => content.Match<byte[]?>(() => null, bytes => bytes);

    [Fact]
    public async Task Add_StoresTheMetadataInTheDatabaseAndTheContentInTheDataDirectory()
    {
        byte[] bytes = [1, 2, 3, 4];

        var id = await _bed.AddVfsFileAsync(VfsTypes.Sites, "logo.png", "image/png", bytes);

        await using var read = _bed.Read();
        var found = Found(await new VfsQueries(read, _bed.ContentStore).Open(VfsTypes.AllSiteImages, id));
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(new VersionedKey<Guid>(id, 1), found.File.Id);
        Assert.Equal((VfsTypes.Sites, "logo.png", "image/png", 4L), (found.File.Type, found.File.FileName, found.File.ContentType, found.File.Length));
        Assert.Equal(bytes, found.Bytes);
        Assert.Equal(bytes, Bytes(await _bed.ContentStore.Read(found.File.StorageKey)));
    }

    [Fact]
    public async Task Add_AuditsTheCreationSoTheCreatedTimeComesFromTheAuditLog()
    {
        var id = await _bed.AddImageAsync();

        Assert.Equal(["Created"], await _bed.ActionsAsync(VfsFile.EntityType, id));
        Assert.Equal(new AuditTimes(Some(NewsTestBed.Now), None), await _bed.TimesAsync<VfsFile>(id, 1));
    }

    [Fact]
    public async Task Add_GivesEveryFileItsOwnKey()
    {
        var first = await _bed.AddImageAsync();
        var second = await _bed.AddImageAsync();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Add_NullUpload_Throws()
    {
        await using var write = _bed.Write();

        await Assert.ThrowsAsync<ArgumentNullException>(() => new VfsCommands(write, _bed.ContentStore).Add(null!));
    }

    [Fact]
    public async Task Add_WhenTheDatabaseSaveFails_LeavesNoContentBehind()
    {
        var write = _bed.Write();
        await write.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => new VfsCommands(write, _bed.ContentStore)
            .Add(new IVfsCommands.Upload(VfsTypes.Sites, "a.png", "image/png", [1])));

        Assert.Empty(Directory.EnumerateFiles(_bed.ContentRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Open_UnknownFile_ReturnsNone()
    {
        await using var read = _bed.Read();

        Assert.Equal(None, await new VfsQueries(read, _bed.ContentStore).Open(VfsTypes.AllSiteImages, Guid.NewGuid()));
    }

    [Fact]
    public async Task Open_OnlyFindsFilesOfTheRequestedType()
    {
        var siteImage = await _bed.AddImageAsync();
        var galleryImage = await _bed.AddGalleryImageAsync();

        await using var read = _bed.Read();
        var queries = new VfsQueries(read, _bed.ContentStore);
        Assert.Equal(None, await queries.Open(VfsTypes.GalleryImages, siteImage));
        Assert.Equal(None, await queries.Open(VfsTypes.AllSiteImages, galleryImage));
        Assert.NotEqual(None, await queries.Open(VfsTypes.AllSiteImages, siteImage));
    }

    [Theory]
    [InlineData(VfsTypes.Sites, VfsTypes.Sites, 1)]
    [InlineData(VfsTypes.News, VfsTypes.News, 1)]
    [InlineData(VfsTypes.NewsCategories, VfsTypes.News, 1)]
    [InlineData(VfsTypes.Articles, VfsTypes.Articles, 1)]
    [InlineData(VfsTypes.ArticleCategories, VfsTypes.Articles, 1)]
    [InlineData(VfsTypes.News, VfsTypes.Articles, 0)]
    [InlineData(VfsTypes.Articles, VfsTypes.News, 0)]
    [InlineData(VfsTypes.Sites, VfsTypes.News, 0)]
    public async Task Open_ServesAFileOnlyThroughTheImagesOfItsOwnContent(string type, string served, int expected)
    {
        var id = await _bed.AddVfsFileAsync(type);
        var servedTypes = served switch
        {
            VfsTypes.News => VfsTypes.NewsImages,
            VfsTypes.Articles => VfsTypes.ArticleImages,
            _ => (IReadOnlyList<string>)[VfsTypes.Sites]
        };

        await using var read = _bed.Read();
        var queries = new VfsQueries(read, _bed.ContentStore);
        Assert.Equal(expected, (await queries.Open(servedTypes, id)).Match(() => 0, _ => 1));
        Assert.Equal(None, await queries.Open(VfsTypes.GalleryImages, id));
    }

    [Theory]
    [InlineData(VfsTypes.Sites)]
    [InlineData(VfsTypes.News)]
    [InlineData(VfsTypes.NewsCategories)]
    [InlineData(VfsTypes.Articles)]
    [InlineData(VfsTypes.ArticleCategories)]
    public async Task Open_TheLegacySiteImageUrlServesEverySiteImageType(string type)
    {
        var id = await _bed.AddVfsFileAsync(type);

        await using var read = _bed.Read();

        Assert.NotEqual(None, await new VfsQueries(read, _bed.ContentStore).Open(VfsTypes.AllSiteImages, id));
    }

    [Fact]
    public async Task Open_WhenTheContentIsMissing_ReturnsNone()
    {
        var id = await _bed.AddImageAsync();
        await _bed.ContentStore.Delete(VfsTestPaths.Of(VfsTypes.Sites, id));

        await using var read = _bed.Read();

        Assert.Equal(None, await new VfsQueries(read, _bed.ContentStore).Open(VfsTypes.AllSiteImages, id));
    }

    [Fact]
    public async Task Delete_RemovesTheMetadataAndTheContentOfTheGivenFilesOnly()
    {
        var removed = await _bed.AddImageAsync();
        var kept = await _bed.AddImageAsync();

        await using (var write = _bed.Write())
        {
            await new VfsCommands(write, _bed.ContentStore).Delete([removed, Guid.NewGuid()]);
        }

        await using var read = _bed.Read();
        var queries = new VfsQueries(read, _bed.ContentStore);
        Assert.Equal(None, await queries.Open(VfsTypes.AllSiteImages, removed));
        Assert.Equal(None, await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Sites, removed)));
        Assert.NotEqual(None, await queries.Open(VfsTypes.AllSiteImages, kept));
        Assert.Equal(1, await read.VfsFiles.CountAsync());
    }

    [Fact]
    public async Task Attach_MovesTheContentUnderTheParentAndInsertsTheNextVersion()
    {
        var parent = Guid.NewGuid();
        var id = await _bed.AddVfsFileAsync(VfsTypes.Sites, "logo.png", content: [4, 5, 6]);

        await AttachAsync(parent, id);

        await using var read = _bed.Read();
        var found = Found(await new VfsQueries(read, _bed.ContentStore).Open(VfsTypes.AllSiteImages, id));
        Assert.Equal((new VersionedKey<Guid>(id, 2), (Guid?)parent), (found.File.Id, found.File.ParentId));
        Assert.Equal(new byte[] { 4, 5, 6 }, found.Bytes);
        Assert.Equal(None, await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Sites, id)));
        Assert.Equal(new byte[] { 4, 5, 6 }, Bytes(await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Sites, id, parent))));
        Assert.Equal(["Created"], await _bed.ActionsAsync(VfsFile.EntityType, id, 2));
    }

    [Fact]
    public async Task Attach_AFileThatHasAParentAlready_IsLeftAlone()
    {
        var first = Guid.NewGuid();
        var id = await _bed.AddVfsFileAsync(VfsTypes.Galleries);
        await AttachAsync(VfsTypes.Galleries, first, id);

        await AttachAsync(VfsTypes.Galleries, Guid.NewGuid(), id);

        await using var read = _bed.Read();
        var found = Found(await new VfsQueries(read, _bed.ContentStore).Open(VfsTypes.GalleryImages, id));
        Assert.Equal((2L, (Guid?)first), (found.File.Id.Version, found.File.ParentId));
        Assert.Equal(2, await read.VfsFiles.CountAsync());
    }

    [Fact]
    public async Task Attach_UnknownKeysAndNoKeys_ChangeNothing()
    {
        var id = await _bed.AddImageAsync();

        await AttachAsync(Guid.NewGuid(), Guid.NewGuid());
        await AttachAsync(Guid.NewGuid());

        await using var read = _bed.Read();
        Assert.Equal(1, await read.VfsFiles.CountAsync());
        Assert.NotEqual(None, await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Sites, id)));
    }

    [Fact]
    public async Task Attach_OnlyAttachesFilesOfTheGivenType()
    {
        var id = await _bed.AddGalleryImageAsync();

        await AttachAsync(VfsTypes.Sites, Guid.NewGuid(), id);

        await using var read = _bed.Read();
        Assert.Equal(1, await read.VfsFiles.CountAsync());
        Assert.NotEqual(None, await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Galleries, id)));
    }

    [Fact]
    public async Task Attach_NullKeys_Throws()
    {
        await using var write = _bed.Write();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => new VfsCommands(write, _bed.ContentStore).Attach(VfsTypes.Sites, Guid.NewGuid(), null!));
    }

    [Fact]
    public async Task Delete_AnAttachedFile_RemovesItsContentFromTheParentFolderAndAllVersions()
    {
        var parent = Guid.NewGuid();
        var id = await _bed.AddVfsFileAsync(VfsTypes.Galleries);
        await AttachAsync(VfsTypes.Galleries, parent, id);

        await using (var write = _bed.Write())
        {
            await new VfsCommands(write, _bed.ContentStore).Delete([id]);
        }

        await using var read = _bed.Read();
        Assert.Equal(None, await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Galleries, id, parent)));
        Assert.Equal(0, await read.VfsFiles.CountAsync());
    }

    private async Task AttachAsync(Guid parent, params Guid[] keys) =>
        await AttachAsync(VfsTypes.Sites, parent, keys);

    private async Task AttachAsync(string type, Guid parent, params Guid[] keys)
    {
        await using var write = _bed.Write();

        await new VfsCommands(write, _bed.ContentStore).Attach(type, parent, keys);
    }

    [Fact]
    public async Task Delete_NullKeys_Throws()
    {
        await using var write = _bed.Write();

        await Assert.ThrowsAsync<ArgumentNullException>(() => new VfsCommands(write, _bed.ContentStore).Delete(null!));
    }
}
