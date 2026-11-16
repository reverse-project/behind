using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Commands;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreDatabase;

public sealed class GalleryCommandsTests : IDisposable
{
    private readonly NewsTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private sealed record StoredGallery(
        long Version,
        string Title,
        string Description,
        Guid Author,
        Guid? Thumbnail,
        bool ThumbnailGenerated);

    private sealed record StoredItem(long Version, string Description, Guid Image, Guid Thumbnail, int Position);

    private async Task<IReadOnlyList<StoredGallery>> VersionsAsync(Guid key)
    {
        await using var ctx = _bed.Read();
        using var scope = ctx.SoftDelete.IncludeDeleted();

        return await ctx.Galleries
            .OfKey(key)
            .OrderBy(gallery => EF.Property<long>(gallery, VersionedColumns.Version))
            .Select(gallery => new StoredGallery(
                EF.Property<long>(gallery, VersionedColumns.Version),
                gallery.Title,
                gallery.Description,
                EF.Property<Guid>(gallery, GalleryConfiguration.AuthorIdProperty),
                EF.Property<Guid?>(gallery, GalleryConfiguration.ThumbnailImageIdProperty),
                EF.Property<bool>(gallery, GalleryConfiguration.ThumbnailGeneratedProperty)))
            .ToListAsync();
    }

    private async Task<IReadOnlyList<StoredItem>> ItemsAsync(Guid key)
    {
        await using var ctx = _bed.Read();

        return await ctx.GalleryItems
            .Where(item => EF.Property<Guid>(item, GalleryItemConfiguration.GalleryIdProperty) == key)
            .OrderBy(item => EF.Property<long>(item, VersionedColumns.Version))
            .ThenBy(item => EF.Property<int>(item, GalleryItemConfiguration.PositionProperty))
            .Select(item => new StoredItem(
                EF.Property<long>(item, VersionedColumns.Version),
                item.Description,
                EF.Property<Guid>(item, GalleryItemConfiguration.ImageIdProperty),
                EF.Property<Guid>(item, GalleryItemConfiguration.ThumbnailIdProperty),
                EF.Property<int>(item, GalleryItemConfiguration.PositionProperty)))
            .ToListAsync();
    }

    private async Task<long?> ActiveVersionAsync(Guid key)
    {
        await using var versions = _bed.ReadVersions();
        var active = await versions.ActiveVersionsAsync(Gallery.EntityType, [key.ToString()]);

        return active.TryGetValue(key.ToString(), out var version) ? version : null;
    }

    [Fact]
    public async Task Create_Publish_StoresVersionOneWithItsItemsAndMakesItLive()
    {
        var items = await _bed.NewGalleryItemsAsync("First", "Second", "Third");

        var key = await _bed.CreateGalleryAsync("Trip", "Summer", items, authorId: _bed.OtherAuthorId);

        var stored = Assert.Single(await VersionsAsync(key));
        Assert.Equal((1L, "Trip", "Summer", _bed.OtherAuthorId, (Guid?)null, true),
            (stored.Version, stored.Title, stored.Description, stored.Author, stored.Thumbnail, stored.ThumbnailGenerated));
        Assert.Equal(
            items.Select((item, position) => new StoredItem(1, item.Description, item.ImageId, item.ThumbnailId, position)),
            await ItemsAsync(key));
        Assert.Equal(1L, await ActiveVersionAsync(key));
        Assert.Equal(["Created"], await _bed.ActionsAsync("Gallery", key));
        Assert.Equal(new AuditTimes(Some(NewsTestBed.Now), None), await _bed.TimesAsync<Gallery>(key, 1));
    }

    [Fact]
    public async Task Create_Draft_StoresVersionOneWithoutMakingItLive()
    {
        var key = await _bed.CreateGalleryAsync(publish: false);

        Assert.Single(await VersionsAsync(key));
        Assert.Null(await ActiveVersionAsync(key));
    }

    [Fact]
    public async Task Create_DraftWithEmptyDescriptions_IsAllowed()
    {
        var items = await _bed.NewGalleryItemsAsync("", "  ");

        var key = await _bed.CreateGalleryAsync(items: items, publish: false);

        Assert.Equal(2, (await ItemsAsync(key)).Count);
    }

    [Fact]
    public async Task Create_WithUploadedThumbnail_StoresItAsNotGenerated()
    {
        var thumbnail = await _bed.AddGalleryImageAsync();

        var key = await _bed.CreateGalleryAsync(thumbnailImageId: thumbnail);

        var stored = Assert.Single(await VersionsAsync(key));
        Assert.Equal((thumbnail, false), (stored.Thumbnail, stored.ThumbnailGenerated));
    }

    [Fact]
    public async Task Create_PublishWithEmptyDescriptions_StoresAndPublishesThem()
    {
        var items = await _bed.NewGalleryItemsAsync("First", " ", "");

        var key = await _bed.CreateGalleryAsync(items: items, publish: true);

        Assert.Equal(["First", " ", ""], (await ItemsAsync(key)).Select(item => item.Description));
        Assert.Equal(1L, await ActiveVersionAsync(key));
    }

    [Fact]
    public async Task Create_WithUnknownImages_IsRejectedAndStoresNothing()
    {
        var known = await _bed.NewGalleryItemAsync();
        IReadOnlyList<IGalleryCommands.GalleryDraft> drafts =
        [
            new("Trip", string.Empty, None, true, [known with { ImageId = Guid.NewGuid() }]),
            new("Trip", string.Empty, None, true, [known with { ThumbnailId = Guid.NewGuid() }]),
            new("Trip", string.Empty, Some(Guid.NewGuid()), false, [known])
        ];

        foreach (var draft in drafts)
        {
            var result = await _bed.WithGalleryCommandsAsync(commands =>
                commands.Create(_bed.AuthorId, draft, false));

            Assert.IsType<GallerySaveResult.UnknownImage>(result);
        }

        await using var read = _bed.Read();
        Assert.Empty(await read.Galleries.ToListAsync());
    }

    [Fact]
    public async Task Create_WithFilesOfAnotherType_IsRejected()
    {
        var siteImage = await _bed.AddImageAsync();
        var galleryImage = await _bed.AddGalleryImageAsync();
        var draft = new IGalleryCommands.GalleryDraft(
            "Trip",
            string.Empty,
            None,
            true,
            [new IGalleryCommands.GalleryItemDraft(siteImage, galleryImage, "x")]);

        var result = await _bed.WithGalleryCommandsAsync(commands => commands.Create(_bed.AuthorId, draft, false));

        Assert.IsType<GallerySaveResult.UnknownImage>(result);
    }

    [Fact]
    public async Task SaveVersion_AddsTheNextVersionWithItsOwnItemsAndKeepsTheAuthor()
    {
        var first = await _bed.SaveGalleryAsync(null, "Trip", authorId: _bed.OtherAuthorId);
        var items = await _bed.NewGalleryItemsAsync("Only");

        var second = await _bed.SaveGalleryAsync(first.Key, "Trip 2", "Autumn", items, publish: false);

        Assert.Equal(new VersionedKey<Guid>(first.Key, 2), second);
        var versions = await VersionsAsync(first.Key);
        Assert.Equal([(1L, "Trip"), (2L, "Trip 2")], versions.Select(item => (item.Version, item.Title)));
        Assert.All(versions, item => Assert.Equal(_bed.OtherAuthorId, item.Author));
        Assert.Equal([1L, 1L, 2L], (await ItemsAsync(first.Key)).Select(item => item.Version));
        Assert.Equal(["Created"], await _bed.ActionsAsync("Gallery", first.Key, 2));
        Assert.Equal(1L, await ActiveVersionAsync(first.Key));
    }

    [Fact]
    public async Task SaveVersion_Publish_MovesTheLiveVersion()
    {
        var first = await _bed.SaveGalleryAsync(null);

        await _bed.SaveGalleryAsync(first.Key, publish: true);

        Assert.Equal(2L, await ActiveVersionAsync(first.Key));
    }

    [Fact]
    public async Task SaveVersion_ReordersItemsByTheDraftOrder()
    {
        var items = await _bed.NewGalleryItemsAsync("A", "B", "C");
        var first = await _bed.SaveGalleryAsync(null, items: items);

        await _bed.SaveGalleryAsync(first.Key, items: [items[2], items[0], items[1]]);

        Assert.Equal(
            ["A", "B", "C", "C", "A", "B"],
            (await ItemsAsync(first.Key)).Select(item => item.Description));
    }

    [Fact]
    public async Task SaveVersion_UnknownOrDeletedGallery_ReturnsNotFound()
    {
        var key = await _bed.CreateGalleryAsync();
        await _bed.WithGalleryCommandsAsync(commands => commands.Delete(key));
        var draft = new IGalleryCommands.GalleryDraft("Trip", string.Empty, None, true, []);

        foreach (var id in new[] { Guid.NewGuid(), key })
        {
            Assert.IsType<GallerySaveResult.NotFound>(
                await _bed.WithGalleryCommandsAsync(commands => commands.SaveVersion(id, draft, true)));
        }
    }

    [Fact]
    public async Task Activate_MakesAnOlderVersionLive()
    {
        var first = await _bed.SaveGalleryAsync(null);
        await _bed.SaveGalleryAsync(first.Key);

        var result = await _bed.WithGalleryCommandsAsync(commands => commands.Activate(first));

        Assert.Equal(first, Assert.IsType<GallerySaveResult.Saved>(result).Id);
        Assert.Equal(1L, await ActiveVersionAsync(first.Key));
    }

    [Fact]
    public async Task Activate_ADraftWithEmptyDescriptions_MakesItLive()
    {
        var items = await _bed.NewGalleryItemsAsync("");
        var draft = await _bed.SaveGalleryAsync(null, items: items, publish: false);

        var result = await _bed.WithGalleryCommandsAsync(commands => commands.Activate(draft));

        Assert.Equal(draft, Assert.IsType<GallerySaveResult.Saved>(result).Id);
        Assert.Equal(1L, await ActiveVersionAsync(draft.Key));
    }

    [Fact]
    public async Task Activate_UnknownVersion_ReturnsNotFound()
    {
        var key = await _bed.CreateGalleryAsync();

        Assert.IsType<GallerySaveResult.NotFound>(
            await _bed.WithGalleryCommandsAsync(commands => commands.Activate(new VersionedKey<Guid>(key, 9))));
        Assert.IsType<GallerySaveResult.NotFound>(
            await _bed.WithGalleryCommandsAsync(commands =>
                commands.Activate(new VersionedKey<Guid>(Guid.NewGuid(), 1))));
    }

    [Fact]
    public async Task Delete_SoftDeletesEveryVersion()
    {
        var first = await _bed.SaveGalleryAsync(null);
        await _bed.SaveGalleryAsync(first.Key, publish: false);

        Assert.True(await _bed.WithGalleryCommandsAsync(commands => commands.Delete(first.Key)));

        await using var read = _bed.Read();
        Assert.Empty(await read.Galleries.ToListAsync());
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("Gallery", first.Key, 1));
        Assert.Equal(["Created", "Deleted"], await _bed.ActionsAsync("Gallery", first.Key, 2));
    }

    [Fact]
    public async Task Delete_UnknownOrAlreadyDeletedGallery_ReturnsFalse()
    {
        var key = await _bed.CreateGalleryAsync();
        await _bed.WithGalleryCommandsAsync(commands => commands.Delete(key));

        Assert.False(await _bed.WithGalleryCommandsAsync(commands => commands.Delete(key)));
        Assert.False(await _bed.WithGalleryCommandsAsync(commands => commands.Delete(Guid.NewGuid())));
    }

    [Fact]
    public async Task Restore_BringsBackEveryVersionAndTheLiveVersion()
    {
        var first = await _bed.SaveGalleryAsync(null);
        await _bed.SaveGalleryAsync(first.Key);
        await _bed.WithGalleryCommandsAsync(commands => commands.Delete(first.Key));

        Assert.True(await _bed.WithGalleryCommandsAsync(commands => commands.Restore(first.Key)));

        await using var read = _bed.Read();
        Assert.Equal(2, (await read.Galleries.ToListAsync()).Count);
        Assert.Equal(2L, await ActiveVersionAsync(first.Key));
        Assert.Equal(["Created", "Deleted", "Restored"], await _bed.ActionsAsync("Gallery", first.Key, 2));
    }

    [Fact]
    public async Task Restore_ActiveOrUnknownGallery_ReturnsFalse()
    {
        var key = await _bed.CreateGalleryAsync();

        Assert.False(await _bed.WithGalleryCommandsAsync(commands => commands.Restore(key)));
        Assert.False(await _bed.WithGalleryCommandsAsync(commands => commands.Restore(Guid.NewGuid())));
    }

    [Fact]
    public async Task HardDelete_RemovesDeletedGalleryWithVersionsItemsAndLiveVersion()
    {
        var first = await _bed.SaveGalleryAsync(null);
        await _bed.SaveGalleryAsync(first.Key);
        var other = await _bed.CreateGalleryAsync("Other");
        await _bed.WithGalleryCommandsAsync(commands => commands.Delete(first.Key));

        var result = await _bed.WithGalleryCommandsAsync(commands => commands.HardDelete(first.Key));

        Assert.Equal(HardDeleteResult.Deleted, result);
        Assert.Empty(await VersionsAsync(first.Key));
        Assert.Empty(await ItemsAsync(first.Key));
        Assert.Null(await ActiveVersionAsync(first.Key));
        Assert.Contains("HardDeleted", await _bed.ActionsAsync("Gallery", first.Key, 1));
        Assert.Equal(2, (await ItemsAsync(other)).Count);
    }

    private async Task<IReadOnlyList<Guid>> StoredImageKeysAsync()
    {
        await using var ctx = _bed.Read();

        return await ctx.VfsFiles
            .InSameKindAs(VfsTypes.Galleries)
            .Select(file => EF.Property<Guid>(file, VersionedColumns.Key))
            .Distinct()
            .ToListAsync();
    }

    [Fact]
    public async Task Create_AttachesItsImagesAndItsThumbnailToTheGallery()
    {
        var item = await _bed.NewGalleryItemAsync();
        var cover = await _bed.AddGalleryImageAsync();

        var key = await _bed.CreateGalleryAsync(items: [item], thumbnailImageId: cover);

        foreach (var image in new[] { item.ImageId, item.ThumbnailId, cover })
        {
            Assert.NotEqual(None, await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Galleries, image, key)));
            Assert.Equal(None, await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Galleries, image)));
        }
    }

    [Fact]
    public async Task SaveVersion_KeepsTheImagesUnderTheGalleryTheyWereFirstAttachedTo()
    {
        var item = await _bed.NewGalleryItemAsync();
        var created = await _bed.SaveGalleryAsync(null, items: [item]);

        await _bed.SaveGalleryAsync(created.Key, "Second", items: [item]);

        Assert.NotEqual(None, await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Galleries, item.ImageId, created.Key)));
        await using var read = _bed.Read();
        Assert.Equal(4, await read.VfsFiles.CountAsync());
    }

    [Fact]
    public async Task HardDelete_RemovesTheImagesOfTheGalleryButKeepsTheOnesOtherGalleriesUse()
    {
        var shared = await _bed.NewGalleryItemAsync("Shared");
        var own = await _bed.NewGalleryItemAsync("Own");
        var cover = await _bed.AddGalleryImageAsync();
        var gone = await _bed.SaveGalleryAsync(null, "Gone", items: [shared, own], thumbnailImageId: cover);
        await _bed.SaveGalleryAsync(gone.Key, "Gone 2", items: [own], thumbnailImageId: cover);
        var kept = await _bed.CreateGalleryAsync("Kept", items: [shared]);
        var unrelated = await _bed.AddGalleryImageAsync();
        await _bed.WithGalleryCommandsAsync(commands => commands.Delete(gone.Key));

        await _bed.WithGalleryCommandsAsync(commands => commands.HardDelete(gone.Key));

        Assert.Equal(
            new[] { shared.ImageId, shared.ThumbnailId, unrelated }.Order(),
            (await StoredImageKeysAsync()).Order());
        Assert.Single(await ItemsAsync(kept));
        Assert.Equal(None, await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Galleries, own.ImageId, gone.Key)));
        Assert.Equal(None, await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Galleries, cover, gone.Key)));
        Assert.NotEqual(None, await _bed.ContentStore.Read(VfsTestPaths.Of(VfsTypes.Galleries, shared.ImageId, gone.Key)));
    }

    [Fact]
    public async Task HardDelete_KeepsAnImageThatADeletedGalleryStillUsesAsItsThumbnail()
    {
        var cover = await _bed.AddGalleryImageAsync();
        var gone = await _bed.CreateGalleryAsync("Gone", items: [], thumbnailImageId: cover);
        var other = await _bed.CreateGalleryAsync("Other", items: [], thumbnailImageId: cover);
        await _bed.WithGalleryCommandsAsync(commands => commands.Delete(gone));
        await _bed.WithGalleryCommandsAsync(commands => commands.Delete(other));

        await _bed.WithGalleryCommandsAsync(commands => commands.HardDelete(gone));

        Assert.Equal([cover], await StoredImageKeysAsync());
    }

    [Fact]
    public async Task HardDelete_ActiveOrUnknownGallery_IsNotFound()
    {
        var key = await _bed.CreateGalleryAsync();

        Assert.Equal(
            HardDeleteResult.NotFound,
            await _bed.WithGalleryCommandsAsync(commands => commands.HardDelete(key)));
        Assert.Equal(
            HardDeleteResult.NotFound,
            await _bed.WithGalleryCommandsAsync(commands => commands.HardDelete(Guid.NewGuid())));
        Assert.Single(await VersionsAsync(key));
        Assert.Equal(2, (await ItemsAsync(key)).Count);
    }
}
