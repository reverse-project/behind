using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IGalleryCommands
{
    public sealed record GalleryItemDraft(Guid ImageId, Guid ThumbnailId, string Description);

    public sealed record GalleryDraft(
        string Title,
        string Description,
        Option<Guid> ThumbnailImageId,
        bool ThumbnailGenerated,
        IReadOnlyList<GalleryItemDraft> Items);

    Task<GallerySaveResult> Create(
        Guid authorId,
        GalleryDraft draft,
        bool publish,
        CancellationToken cancellationToken = default);

    Task<GallerySaveResult> SaveVersion(
        Guid galleryId,
        GalleryDraft draft,
        bool publish,
        CancellationToken cancellationToken = default);

    Task<GallerySaveResult> Activate(VersionedKey<Guid> id, CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid galleryId, CancellationToken cancellationToken = default);

    Task<bool> Restore(Guid galleryId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid galleryId, CancellationToken cancellationToken = default);
}
