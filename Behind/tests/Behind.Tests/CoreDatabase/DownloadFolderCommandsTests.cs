using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class DownloadFolderCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private static IDownloadFolderCommands.FolderDraft Draft(string slug = "manuals", string name = "Manuals") =>
        new(slug, name);

    private async Task<IReadOnlyList<(long Version, string Slug, string Name, Guid? ParentId)>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();
        var rows = await ctx.DownloadFolders
            .OfKey(key)
            .OrderBy(folder => EF.Property<long>(folder, VersionedColumns.Version))
            .Select(folder => new
            {
                Version = EF.Property<long>(folder, VersionedColumns.Version),
                folder.Slug,
                folder.DisplayName,
                ParentId = EF.Property<Guid?>(folder, DownloadFolderConfiguration.ParentIdProperty)
            })
            .ToListAsync();

        return rows.Select(row => (row.Version, row.Slug, row.DisplayName, row.ParentId)).ToList();
    }

    [Fact]
    public async Task Create_StoresVersionOneAndAuditsTheCreation()
    {
        var key = await _bed.CreateDownloadFolderAsync();

        Assert.Equal([(1L, "manuals", "Manuals", (Guid?)null)], await VersionsAsync(key));
        Assert.Equal(["Created"], await _bed.ActionsAsync("DownloadFolder", key));
    }

    [Fact]
    public async Task Create_InsideAFolder_StoresTheParent()
    {
        var parent = await _bed.CreateDownloadFolderAsync();
        var child = await _bed.CreateDownloadFolderAsync("Drivers", "drivers", parent);

        Assert.Equal(parent, Assert.Single(await VersionsAsync(child)).ParentId);
    }

    [Fact]
    public async Task Create_InsideAnUnknownOrDeletedFolder_IsRejected()
    {
        var parent = await _bed.CreateDownloadFolderAsync();
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(parent));

        Assert.IsType<DownloadFolderSaveResult.ParentNotFound>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Create(Guid.NewGuid(), Draft())));
        Assert.IsType<DownloadFolderSaveResult.ParentNotFound>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Create(parent, Draft("a", "A"))));
    }

    [Fact]
    public async Task Create_SlugOfASiblingIgnoringCase_IsRejected()
    {
        var parent = await _bed.CreateDownloadFolderAsync();
        await _bed.CreateDownloadFolderAsync("Drivers", "drivers", parent);

        Assert.IsType<DownloadFolderSaveResult.SlugTaken>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Create(parent, Draft("DRIVERS", "Other"))));
    }

    [Fact]
    public async Task Create_SameSlugInAnotherFolderOrAtTheRoot_IsAllowed()
    {
        var first = await _bed.CreateDownloadFolderAsync("First", "first");
        var second = await _bed.CreateDownloadFolderAsync("Second", "second");
        await _bed.CreateDownloadFolderAsync("Drivers", "drivers", first);

        Assert.IsType<DownloadFolderSaveResult.Saved>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Create(second, Draft("drivers", "Drivers"))));
        Assert.IsType<DownloadFolderSaveResult.Saved>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Create(None, Draft("drivers", "Drivers"))));
    }

    [Fact]
    public async Task Update_InsertsTheNextVersionAndKeepsTheParent()
    {
        var parent = await _bed.CreateDownloadFolderAsync();
        var key = await _bed.CreateDownloadFolderAsync("Drivers", "drivers", parent);

        var result = await _bed.WithDownloadFolderCommandsAsync(commands => commands.Update(key, Draft("tools", "Tools")));

        Assert.Equal(new VersionedKey<Guid>(key, 2), Assert.IsType<DownloadFolderSaveResult.Saved>(result).Id);
        Assert.Equal(
            [(1L, "drivers", "Drivers", (Guid?)parent), (2L, "tools", "Tools", (Guid?)parent)],
            await VersionsAsync(key));
    }

    [Fact]
    public async Task Update_KeepingItsOwnSlug_IsAllowed()
    {
        var key = await _bed.CreateDownloadFolderAsync();

        Assert.IsType<DownloadFolderSaveResult.Saved>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Update(key, Draft("manuals", "Guides"))));
    }

    [Fact]
    public async Task Update_SlugOfASibling_IsRejected()
    {
        await _bed.CreateDownloadFolderAsync("Drivers", "drivers");
        var other = await _bed.CreateDownloadFolderAsync("Tools", "tools");

        Assert.IsType<DownloadFolderSaveResult.SlugTaken>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Update(other, Draft("drivers", "Tools"))));
    }

    [Fact]
    public async Task Update_UnknownOrDeletedFolder_IsNotFound()
    {
        var key = await _bed.CreateDownloadFolderAsync();
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(key));

        Assert.IsType<DownloadFolderSaveResult.NotFound>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Update(Guid.NewGuid(), Draft())));
        Assert.IsType<DownloadFolderSaveResult.NotFound>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Update(key, Draft())));
    }

    [Fact]
    public async Task Delete_SoftDeletesTheFolder()
    {
        var key = await _bed.CreateDownloadFolderAsync();

        Assert.True(await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(key)));

        Assert.Empty(await _bed.WithDownloadQueriesAsync(queries => queries.ListFolders()));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("DownloadFolder", key));
    }

    [Fact]
    public async Task Delete_UnknownFolder_ReturnsFalse()
    {
        Assert.False(await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(Guid.NewGuid())));
    }

    [Fact]
    public async Task Restore_BringsTheFolderBack()
    {
        var key = await _bed.CreateDownloadFolderAsync();
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(key));

        var result = await _bed.WithDownloadFolderCommandsAsync(commands => commands.Restore(key));

        Assert.Equal(new VersionedKey<Guid>(key, 1), Assert.IsType<DownloadFolderSaveResult.Saved>(result).Id);
        Assert.Single(await _bed.WithDownloadQueriesAsync(queries => queries.ListFolders()));
    }

    [Fact]
    public async Task Restore_ActiveOrUnknownFolder_IsNotFound()
    {
        var key = await _bed.CreateDownloadFolderAsync();

        Assert.IsType<DownloadFolderSaveResult.NotFound>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Restore(key)));
        Assert.IsType<DownloadFolderSaveResult.NotFound>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Restore(Guid.NewGuid())));
    }

    [Fact]
    public async Task Restore_WhileTheParentIsDeleted_IsRejected()
    {
        var parent = await _bed.CreateDownloadFolderAsync();
        var child = await _bed.CreateDownloadFolderAsync("Drivers", "drivers", parent);
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(child));
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(parent));

        Assert.IsType<DownloadFolderSaveResult.ParentDeleted>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Restore(child)));
    }

    [Fact]
    public async Task Restore_WhileASiblingUsesTheSlug_IsRejected()
    {
        var key = await _bed.CreateDownloadFolderAsync();
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(key));
        await _bed.CreateDownloadFolderAsync("Other manuals", "manuals");

        Assert.IsType<DownloadFolderSaveResult.SlugTaken>(
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.Restore(key)));
    }

    [Fact]
    public async Task HardDelete_OfAnActiveFolder_IsNotFound()
    {
        var key = await _bed.CreateDownloadFolderAsync();

        Assert.Equal(
            HardDeleteResult.NotFound,
            await _bed.WithDownloadFolderCommandsAsync(commands => commands.HardDelete(key)));
    }

    [Fact]
    public async Task HardDelete_RemovesTheFolderItsSubfoldersAndTheirFilesWithTheirContent()
    {
        var parent = await _bed.CreateDownloadFolderAsync();
        var child = await _bed.CreateDownloadFolderAsync("Drivers", "drivers", parent);
        var sibling = await _bed.CreateDownloadFolderAsync("Other", "other");
        var inParent = await _bed.UploadDownloadAsync(parent, "Guide", "guide.pdf");
        var inChild = await _bed.UploadDownloadAsync(child, "Driver", "driver.zip");
        var kept = await _bed.UploadDownloadAsync(sibling, "Kept", "kept.txt");
        await using var read = _bed.Read();
        var fileOf = async (Guid download) => await read.Downloads
            .OfKey(download)
            .Select(row => EF.Property<Guid>(row, DownloadConfiguration.FileIdProperty))
            .FirstAsync();
        var parentFile = await fileOf(inParent);
        var childFile = await fileOf(inChild);
        var keptFile = await fileOf(kept);
        await _bed.WithDownloadFolderCommandsAsync(commands => commands.Delete(parent));

        var result = await _bed.WithDownloadFolderCommandsAsync(commands => commands.HardDelete(parent));

        Assert.Equal(HardDeleteResult.Deleted, result);
        await using var after = _bed.Read();
        using var scope = after.SoftDelete.IncludeDeleted();
        Assert.Equal(
            [sibling],
            await after.DownloadFolders.Select(folder => EF.Property<Guid>(folder, VersionedColumns.Key)).ToListAsync());
        Assert.Equal(
            [kept],
            await after.Downloads.Select(download => EF.Property<Guid>(download, VersionedColumns.Key)).ToListAsync());
        Assert.False(await _bed.StoredAsync(VfsTestPaths.Of(VfsTypes.Downloads, parentFile, inParent)));
        Assert.False(await _bed.StoredAsync(VfsTestPaths.Of(VfsTypes.Downloads, childFile, inChild)));
        Assert.True(await _bed.StoredAsync(VfsTestPaths.Of(VfsTypes.Downloads, keptFile, kept)));
        Assert.Equal(["Created", "Deleted", "HardDeleted"], await _bed.ActionsAsync("DownloadFolder", parent));
    }
}
