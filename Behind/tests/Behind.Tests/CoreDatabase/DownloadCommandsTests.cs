using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class DownloadCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<Guid> FileOfAsync(Guid download)
    {
        await using var ctx = _bed.Read();

        return await ctx.Downloads
            .OfKey(download)
            .Select(row => EF.Property<Guid>(row, DownloadConfiguration.FileIdProperty))
            .FirstAsync();
    }

    private async Task<IReadOnlyList<IDownloadQueries.DownloadInfo>> FilesOfAsync(Guid folder) =>
        (await _bed.WithDownloadQueriesAsync(queries => queries.ListFiles(folder))).Match(() => [], files => files);

    private async Task<IReadOnlyList<(long Version, string Name)>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();
        var rows = await ctx.Downloads
            .OfKey(key)
            .OrderBy(row => EF.Property<long>(row, VersionedColumns.Version))
            .Select(row => new { Version = EF.Property<long>(row, VersionedColumns.Version), row.DisplayName })
            .ToListAsync();

        return rows.Select(row => (row.Version, row.DisplayName)).ToList();
    }

    [Fact]
    public async Task Add_StoresTheDownloadAndTheFileUnderTheDownload()
    {
        var folder = await _bed.CreateDownloadFolderAsync();

        var key = await _bed.UploadDownloadAsync(folder, "User guide", "guide.pdf", "application/pdf", [9, 8, 7]);

        var file = await FileOfAsync(key);
        Assert.Equal([(1L, "User guide")], await VersionsAsync(key));
        Assert.Equal([9, 8, 7], (await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Downloads, file, key))).Match(() => [], bytes => bytes));
        Assert.False(await _bed.StoredAsync(VfsTestPaths.Of(VfsTypes.Downloads, file)));
        Assert.Equal(["Created"], await _bed.ActionsAsync("Download", key));
        await using var ctx = _bed.Read();
        var stored = await ctx.VfsFiles.OfKey(file).ToFilesAsync();
        var latest = stored.Latest().Single();
        Assert.Equal(("guide.pdf", "application/pdf", 3L, (Guid?)key), (latest.FileName, latest.ContentType, latest.Length, latest.ParentId));
    }

    [Fact]
    public async Task Add_BlankDisplayName_DefaultsToTheFileName()
    {
        var folder = await _bed.CreateDownloadFolderAsync();

        var key = await _bed.UploadDownloadAsync(folder, "  ", "report.xlsx");

        Assert.Equal([(1L, "report.xlsx")], await VersionsAsync(key));
    }

    [Fact]
    public async Task Add_LongDisplayName_IsShortened()
    {
        var folder = await _bed.CreateDownloadFolderAsync();

        var key = await _bed.UploadDownloadAsync(folder, new string('n', 500));

        Assert.Equal(Download.MaxDisplayNameLength, Assert.Single(await VersionsAsync(key)).Name.Length);
    }

    [Fact]
    public async Task Add_ToAnUnknownOrDeletedFolder_IsRejectedWithoutStoringTheFile()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(folder));
        var draft = new IDownloadCommands.FileDraft("Guide", "guide.pdf", "application/pdf", [1]);

        Assert.IsType<DownloadSaveResult.FolderNotFound>(
            await _bed.WithDownloadCommandsAsync(commands => commands.Add(Guid.NewGuid(), _bed.AuthorId, draft)));
        Assert.IsType<DownloadSaveResult.FolderNotFound>(
            await _bed.WithDownloadCommandsAsync(commands => commands.Add(folder, _bed.AuthorId, draft)));

        await using var ctx = _bed.Read();
        Assert.Empty(await ctx.VfsFiles.ToListAsync());
    }

    [Fact]
    public async Task Rename_InsertsTheNextVersionAndKeepsTheFile()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var key = await _bed.UploadDownloadAsync(folder, "Guide");
        var file = await FileOfAsync(key);

        var result = await _bed.WithDownloadCommandsAsync(commands => commands.Rename(key, "Handbook"));

        Assert.Equal(new VersionedKey<Guid>(key, 2), Assert.IsType<DownloadSaveResult.Saved>(result).Id);
        Assert.Equal([(1L, "Guide"), (2L, "Handbook")], await VersionsAsync(key));
        Assert.Equal(file, await FileOfAsync(key));
        Assert.True(await _bed.StoredAsync(VfsTestPaths.Of(VfsTypes.Downloads, file, key)));
    }

    [Fact]
    public async Task Rename_BlankName_KeepsTheCurrentName()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var key = await _bed.UploadDownloadAsync(folder, "Guide");

        await _bed.WithDownloadCommandsAsync(commands => commands.Rename(key, " "));

        Assert.Equal([(1L, "Guide"), (2L, "Guide")], await VersionsAsync(key));
    }

    [Fact]
    public async Task Rename_UnknownOrDeletedDownload_IsNotFound()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var key = await _bed.UploadDownloadAsync(folder);
        await _bed.WithDownloadCommandsAsync(commands => commands.Delete(key));

        Assert.IsType<DownloadSaveResult.NotFound>(
            await _bed.WithDownloadCommandsAsync(commands => commands.Rename(Guid.NewGuid(), "x")));
        Assert.IsType<DownloadSaveResult.NotFound>(
            await _bed.WithDownloadCommandsAsync(commands => commands.Rename(key, "x")));
    }

    [Fact]
    public async Task Delete_SoftDeletesAndKeepsTheContent()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var key = await _bed.UploadDownloadAsync(folder);
        var file = await FileOfAsync(key);

        Assert.True(await _bed.WithDownloadCommandsAsync(commands => commands.Delete(key)));
        Assert.False(await _bed.WithDownloadCommandsAsync(commands => commands.Delete(Guid.NewGuid())));

        Assert.Empty(await FilesOfAsync(folder));
        Assert.True(await _bed.StoredAsync(VfsTestPaths.Of(VfsTypes.Downloads, file, key)));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("Download", key));
    }

    [Fact]
    public async Task Restore_BringsTheDownloadBack()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var key = await _bed.UploadDownloadAsync(folder);
        await _bed.WithDownloadCommandsAsync(commands => commands.Delete(key));

        var result = await _bed.WithDownloadCommandsAsync(commands => commands.Restore(key));

        Assert.Equal(new VersionedKey<Guid>(key, 1), Assert.IsType<DownloadSaveResult.Saved>(result).Id);
        Assert.Single(await FilesOfAsync(folder));
    }

    [Fact]
    public async Task Restore_ActiveOrUnknownDownload_IsNotFound()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var key = await _bed.UploadDownloadAsync(folder);

        Assert.IsType<DownloadSaveResult.NotFound>(await _bed.WithDownloadCommandsAsync(commands => commands.Restore(key)));
        Assert.IsType<DownloadSaveResult.NotFound>(
            await _bed.WithDownloadCommandsAsync(commands => commands.Restore(Guid.NewGuid())));
    }

    [Fact]
    public async Task Restore_WhileTheFolderIsDeleted_IsRejected()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var key = await _bed.UploadDownloadAsync(folder);
        await _bed.WithDownloadCommandsAsync(commands => commands.Delete(key));
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(folder));

        Assert.IsType<DownloadSaveResult.FolderDeleted>(
            await _bed.WithDownloadCommandsAsync(commands => commands.Restore(key)));
    }

    [Fact]
    public async Task HardDelete_OfAnActiveDownload_IsNotFound()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var key = await _bed.UploadDownloadAsync(folder);

        Assert.Equal(HardDeleteResult.NotFound, await _bed.WithDownloadCommandsAsync(commands => commands.HardDelete(key)));
    }

    [Fact]
    public async Task HardDelete_RemovesTheDownloadItsFileAndItsContent()
    {
        var folder = await _bed.CreateDownloadFolderAsync();
        var key = await _bed.UploadDownloadAsync(folder);
        var other = await _bed.UploadDownloadAsync(folder, "Other", "other.txt");
        var file = await FileOfAsync(key);
        await _bed.WithDownloadCommandsAsync(commands => commands.Delete(key));

        var result = await _bed.WithDownloadCommandsAsync(commands => commands.HardDelete(key));

        Assert.Equal(HardDeleteResult.Deleted, result);
        Assert.False(await _bed.StoredAsync(VfsTestPaths.Of(VfsTypes.Downloads, file, key)));
        await using var ctx = _bed.Read();
        using var scope = ctx.SoftDelete.IncludeDeleted();
        Assert.Equal(
            [other],
            await ctx.Downloads.Select(row => EF.Property<Guid>(row, VersionedColumns.Key)).ToListAsync());
        Assert.Empty(await ctx.VfsFiles.OfKey(file).ToListAsync());
    }
}
