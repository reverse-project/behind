using Behind.Core.Database;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class DownloadQueriesTests : IDisposable
{
    private static readonly DateTimeOffset UploadedAt = new(2026, 11, 1, 8, 30, 0, TimeSpan.Zero);

    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private Task<Option<IReadOnlyList<IDownloadQueries.DownloadInfo>>> FilesAsync(Guid folder) =>
        _bed.WithDownloadQueriesAsync(queries => queries.ListFiles(folder));

    [Fact]
    public async Task ListFolders_ReturnsTheActiveFoldersInDisplayOrder()
    {
        var zebra = await _bed.CreateDownloadFolderAsync("Zebra", "zebra");
        var apple = await _bed.CreateDownloadFolderAsync("apple", "apple");
        var nested = await _bed.CreateDownloadFolderAsync("Nested", "nested", apple);

        var folders = await _bed.WithDownloadQueriesAsync(queries => queries.ListFolders());

        Assert.Equal(["apple", "Nested", "Zebra"], folders.Select(folder => folder.DisplayName));
        Assert.Equal([apple, nested, zebra], folders.Select(folder => folder.Id));
        Assert.Equal(Some(apple), folders[1].ParentId);
        Assert.Equal(None, folders[0].ParentId);
    }

    [Fact]
    public async Task ListFolders_ReturnsTheLatestVersionOfEveryFolder()
    {
        var key = await _bed.CreateDownloadFolderAsync("Old", "old");
        await _bed.WithDownloadFolderCommandsAsync(
            commands => commands.Update(key, new IDownloadFolderCommands.FolderDraft("new", "New")));

        var folder = Assert.Single(await _bed.WithDownloadQueriesAsync(queries => queries.ListFolders()));

        Assert.Equal(("new", "New"), (folder.Slug, folder.DisplayName));
    }

    [Fact]
    public async Task ListFolders_HidesDeletedFoldersAndEverythingBelowThem()
    {
        var parent = await _bed.CreateDownloadFolderAsync("Parent", "parent");
        await _bed.CreateDownloadFolderAsync("Child", "child", parent);
        var kept = await _bed.CreateDownloadFolderAsync("Kept", "kept");
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(parent));

        var folders = await _bed.WithDownloadQueriesAsync(queries => queries.ListFolders());

        Assert.Equal([kept], folders.Select(folder => folder.Id));
    }

    [Fact]
    public async Task ListFiles_DescribesTheFilesOfAFolderInNameOrder()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        await _bed.UploadDownloadAsync(folder, "Zed", "zed.txt", "text/plain", [1, 2], at: UploadedAt);
        var guide = await _bed.UploadDownloadAsync(
            folder,
            "guide",
            "manual.pdf",
            "application/pdf",
            new byte[2048],
            _bed.OtherAuthorId,
            UploadedAt.AddDays(1));

        var files = (await FilesAsync(folder)).Match(() => [], found => found);

        Assert.Equal(["guide", "Zed"], files.Select(file => file.DisplayName));
        Assert.Equal(
            new IDownloadQueries.DownloadInfo(
                guide,
                folder,
                "guide",
                "manual.pdf",
                "application/pdf",
                2048,
                _bed.OtherAuthorId.ToUserLabel("Test User"),
                Some(UploadedAt.AddDays(1))) { UploadedById = _bed.OtherAuthorId },
            files[0]);
        Assert.Equal(2, files[1].Length);
    }

    [Fact]
    public async Task ListFiles_RenamedFile_KeepsTheUploadTimeAndShowsTheNewName()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var key = await _bed.UploadDownloadAsync(folder, "Guide", at: UploadedAt);
        await _bed.WithDownloadCommandsAsync(commands => commands.Rename(key, "Handbook"), UploadedAt.AddDays(3));

        var file = Assert.Single((await FilesAsync(folder)).Match(() => [], found => found));

        Assert.Equal(("Handbook", Some(UploadedAt)), (file.DisplayName, file.UploadedAt));
    }

    [Fact]
    public async Task ListFiles_HidesDeletedFilesAndOtherFolders()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var other = await _bed.CreateDownloadFolderAsync("Other", "other");
        var gone = await _bed.UploadDownloadAsync(folder, "Gone");
        var kept = await _bed.UploadDownloadAsync(folder, "Kept");
        await _bed.UploadDownloadAsync(other, "Elsewhere");
        await _bed.WithDownloadCommandsAsync(commands => commands.Delete(gone));

        var files = (await FilesAsync(folder)).Match(() => [], found => found);

        Assert.Equal([kept], files.Select(file => file.Id));
    }

    [Fact]
    public async Task ListFiles_UnknownDeletedOrHiddenFolder_IsNone()
    {
        var parent = await _bed.CreateDownloadFolderAsync();
        var child = await _bed.CreateDownloadFolderAsync("Child", "child", parent);
        var deleted = await _bed.CreateDownloadFolderAsync("Deleted", "deleted");
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(deleted));
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(parent));

        Assert.Equal(None, await FilesAsync(Guid.NewGuid()));
        Assert.Equal(None, await FilesAsync(deleted));
        Assert.Equal(None, await FilesAsync(child));
    }

    [Fact]
    public async Task ListDeleted_ReturnsTheDeletedFoldersAndFiles()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var deletedFolder = await _bed.CreateDownloadFolderAsync("Gone folder", "gone");
        var deletedFile = await _bed.UploadDownloadAsync(folder, "Gone file");
        await _bed.UploadDownloadAsync(folder, "Kept file");
        await _bed.WithDownloadCommandsAsync(commands => commands.Delete(deletedFile));
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(deletedFolder));

        var deleted = await _bed.WithDownloadQueriesAsync(queries => queries.ListDeleted());

        Assert.Equal([deletedFolder], deleted.Folders.Select(item => item.Id));
        Assert.Equal(["Gone file"], deleted.Files.Select(item => item.DisplayName));
        Assert.Equal(folder, Assert.Single(deleted.Files).FolderId);
    }

    [Fact]
    public async Task Open_ReturnsTheFileAndItsContent()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var key = await _bed.UploadDownloadAsync(folder, "Guide", "guide.pdf", "application/pdf", [4, 5, 6]);

        var content = await _bed.WithDownloadQueriesAsync(queries => queries.Open(key));

        var found = content.Match(() => throw new InvalidOperationException(), value => value);
        Assert.Equal(("guide.pdf", "application/pdf"), (found.File.FileName, found.File.ContentType));
        Assert.Equal([4, 5, 6], found.Bytes);
    }

    [Fact]
    public async Task Open_UnknownDeletedOrHiddenDownload_IsNone()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var deleted = await _bed.UploadDownloadAsync(folder, "Deleted");
        var hidden = await _bed.UploadDownloadAsync(folder, "Hidden");
        await _bed.WithDownloadCommandsAsync(commands => commands.Delete(deleted));
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(folder));

        Assert.Equal(None, await _bed.WithDownloadQueriesAsync(queries => queries.Open(Guid.NewGuid())));
        Assert.Equal(None, await _bed.WithDownloadQueriesAsync(queries => queries.Open(deleted)));
        Assert.Equal(None, await _bed.WithDownloadQueriesAsync(queries => queries.Open(hidden)));
    }
}
