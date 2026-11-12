using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class GalleryCommands(
    AbstractCoreContext context,
    AbstractVersioningContext versions,
    IVfsCommands files) : IGalleryCommands
{
    public Task<GallerySaveResult> Create(
        Guid authorId,
        IGalleryCommands.GalleryDraft draft,
        bool publish,
        CancellationToken cancellationToken = default) =>
        AddVersion(Guid.CreateVersion7(), 0, authorId, draft, publish, cancellationToken);

    public async Task<GallerySaveResult> SaveVersion(
        Guid galleryId,
        IGalleryCommands.GalleryDraft draft,
        bool publish,
        CancellationToken cancellationToken = default)
    {
        var latest = (await context.ActiveGalleries()
                .OfKey(galleryId)
                .OrderByDescending(gallery => EF.Property<long>(gallery, VersionedColumns.Version))
                .ToRows()
                .Take(1)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Head();

        return await latest.Match(
            () => Task.FromResult<GallerySaveResult>(new GallerySaveResult.NotFound()),
            row => AddVersion(galleryId, row.Version, row.AuthorId, draft, publish, cancellationToken));
    }

    public async Task<GallerySaveResult> Activate(
        VersionedKey<Guid> id,
        CancellationToken cancellationToken = default)
    {
        var exists = await context.ActiveGalleries().OfVersion(id).AnyAsync(cancellationToken);

        if (!exists)
        {
            return new GallerySaveResult.NotFound();
        }

        await SetActive(id, cancellationToken);

        return new GallerySaveResult.Saved(id);
    }

    public async Task<bool> Delete(Guid galleryId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkSoftDeletedAsync(context.Galleries.OfKey(galleryId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<bool> Restore(Guid galleryId, CancellationToken cancellationToken = default)
    {
        var restored = await context.MarkRestoredAsync(context.Galleries.OfKey(galleryId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return restored > 0;
    }

    public async Task<HardDeleteResult> HardDelete(Guid galleryId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkHardDeletedAsync(context.Galleries.OfKey(galleryId), cancellationToken);

        if (deleted == 0)
        {
            return HardDeleteResult.NotFound;
        }

        var items = await context.GalleryItems
            .Where(item => EF.Property<Guid>(item, GalleryItemConfiguration.GalleryIdProperty) == galleryId)
            .ToListAsync(cancellationToken);

        var imageIds = await ImageIdsOf(galleryId, items, cancellationToken);

        var unused = await UnusedImages(galleryId, imageIds, cancellationToken);

        context.GalleryItems.RemoveRange(items);

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            await versions.ClearActiveVersionAsync(Gallery.EntityType, galleryId.ToString(), cancellationToken);
            await versions.SaveChangesAsync(cancellationToken);

            await files.Delete(unused, cancellationToken);

            return HardDeleteResult.Deleted;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return HardDeleteResult.Rejected;
        }
    }

    private async Task<GallerySaveResult> AddVersion(
        Guid key,
        long previousVersion,
        Guid authorId,
        IGalleryCommands.GalleryDraft draft,
        bool publish,
        CancellationToken cancellationToken)
    {
        if (!await HasImages(draft, cancellationToken)
            || !await context.HasContentChildren<GalleryItem>(key, GalleryItemConfiguration.GalleryIdProperty,
                draft.Items.Select(item => item.Id), cancellationToken))
        {
            return new GallerySaveResult.UnknownImage();
        }

        var id = new VersionedKey<Guid>(key, previousVersion + 1);
        var gallery = new Gallery(draft.Title, draft.Description);

        await context.Galleries.AddAsync(gallery, cancellationToken);

        var entry = context.Entry(gallery);
        entry.StampVersionedKey(id.Key, id.Version);
        entry.Property<Guid>(GalleryConfiguration.AuthorIdProperty).CurrentValue = authorId;
        entry.Property<Guid?>(GalleryConfiguration.ThumbnailImageIdProperty).CurrentValue =
            draft.ThumbnailImageId.ToNullable();
        entry.Property<bool>(GalleryConfiguration.ThumbnailGeneratedProperty).CurrentValue =
            draft.ThumbnailGenerated;

        foreach (var (item, position) in draft.Items.Select((item, position) => (item, position)))
        {
            await AddItem(id, item, position, cancellationToken);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new GallerySaveResult.VersionConflict();
        }

        await files.Attach(VfsTypes.Galleries, id.Key, ImageKeysOf(draft), cancellationToken);

        if (publish)
        {
            await SetActive(id, cancellationToken);
        }

        return new GallerySaveResult.Saved(id);
    }

    private async Task AddItem(
        VersionedKey<Guid> gallery,
        IGalleryCommands.GalleryItemDraft draft,
        int position,
        CancellationToken cancellationToken)
    {
        var item = new GalleryItem(draft.Description);

        await context.GalleryItems.AddAsync(item, cancellationToken);

        var entry = context.Entry(item);
        entry.StampVersionedKey(draft.Id ?? Guid.CreateVersion7(), gallery.Version);
        entry.Property<Guid>(GalleryItemConfiguration.GalleryIdProperty).CurrentValue = gallery.Key;
        entry.Property<Guid>(GalleryItemConfiguration.ImageIdProperty).CurrentValue = draft.ImageId;
        entry.Property<Guid>(GalleryItemConfiguration.ThumbnailIdProperty).CurrentValue = draft.ThumbnailId;
        entry.Property<int>(GalleryItemConfiguration.PositionProperty).CurrentValue = position;
    }

    private async Task SetActive(VersionedKey<Guid> id, CancellationToken cancellationToken)
    {
        await versions.SetActiveVersionAsync(Gallery.EntityType, id.Key.ToString(), id.Version, cancellationToken);
        await versions.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<Guid> ImageKeysOf(IGalleryCommands.GalleryDraft draft) =>
        draft.Items
            .SelectMany(item => new[] { item.ImageId, item.ThumbnailId })
            .Concat(draft.ThumbnailImageId.AsEnumerable())
            .Distinct()
            .ToList();

    private async Task<bool> HasImages(IGalleryCommands.GalleryDraft draft, CancellationToken cancellationToken)
    {
        var wanted = ImageKeysOf(draft);

        var found = await context.VfsFiles
            .InSameKindAs(VfsTypes.Galleries)
            .OfKeys(wanted)
            .Select(file => EF.Property<Guid>(file, VersionedColumns.Key))
            .Distinct()
            .CountAsync(cancellationToken);

        return found == wanted.Count;
    }

    private async Task<IReadOnlyList<Guid>> ImageIdsOf(
        Guid galleryId,
        IReadOnlyList<GalleryItem> items,
        CancellationToken cancellationToken)
    {
        List<Guid?> covers;

        using (context.SoftDelete.IncludeDeleted())
        {
            covers = await context.Galleries
                .OfKey(galleryId)
                .Select(gallery => EF.Property<Guid?>(gallery, GalleryConfiguration.ThumbnailImageIdProperty))
                .ToListAsync(cancellationToken);
        }

        return items
            .SelectMany(item => new[]
            {
                context.Entry(item).Property<Guid>(GalleryItemConfiguration.ImageIdProperty).CurrentValue,
                context.Entry(item).Property<Guid>(GalleryItemConfiguration.ThumbnailIdProperty).CurrentValue
            })
            .Concat(covers.OfType<Guid>())
            .Distinct()
            .ToList();
    }

    private async Task<IReadOnlyCollection<Guid>> UnusedImages(
        Guid galleryId,
        IReadOnlyList<Guid> imageIds,
        CancellationToken cancellationToken)
    {
        var usedByItems = await context.GalleryItems
            .Where(item => EF.Property<Guid>(item, GalleryItemConfiguration.GalleryIdProperty) != galleryId
                           && (imageIds.Contains(EF.Property<Guid>(item, GalleryItemConfiguration.ImageIdProperty))
                               || imageIds.Contains(EF.Property<Guid>(item, GalleryItemConfiguration.ThumbnailIdProperty))))
            .Select(item => new
            {
                Image = EF.Property<Guid>(item, GalleryItemConfiguration.ImageIdProperty),
                Thumbnail = EF.Property<Guid>(item, GalleryItemConfiguration.ThumbnailIdProperty)
            })
            .ToListAsync(cancellationToken);

        List<Guid?> usedByGalleries;

        using (context.SoftDelete.IncludeDeleted())
        {
            usedByGalleries = await context.Galleries
                .Where(gallery => EF.Property<Guid>(gallery, VersionedColumns.Key) != galleryId
                                  && EF.Property<Guid?>(gallery, GalleryConfiguration.ThumbnailImageIdProperty) != null
                                  && imageIds.Contains(EF.Property<Guid?>(gallery, GalleryConfiguration.ThumbnailImageIdProperty)!.Value))
                .Select(gallery => EF.Property<Guid?>(gallery, GalleryConfiguration.ThumbnailImageIdProperty))
                .ToListAsync(cancellationToken);
        }

        var used = usedByItems.SelectMany(item => new[] { item.Image, item.Thumbnail }).Concat(usedByGalleries.OfType<Guid>()).ToHashSet();

        return imageIds.Where(id => !used.Contains(id)).ToList();
    }
}
