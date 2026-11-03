using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class GalleryResponseExtensions
{
    public const string UnsupportedImageError = "unsupported";
    public const string TooLargeImageError = "too-large";
    public const string UnreadableImageError = "unreadable";

    extension(Guid imageId)
    {
        public string ToImageUrl() => $"{GalleryImageEndpoints.ImagePath}/{imageId}";
    }

    extension(Option<Guid> imageId)
    {
        public string? ToImageUrlOrNull() => imageId.Match<string?>(() => null, id => id.ToImageUrl());
    }

    extension(IGalleryQueries.GalleryListing gallery)
    {
        public GalleryListItem ToItem() =>
            new(
                gallery.Id.Key,
                gallery.Id.Version,
                gallery.Title,
                gallery.Description,
                gallery.Author,
                gallery.CreatedAt.ToNullable(),
                gallery.UpdatedAt.ToNullable(),
                gallery.ThumbnailId.ToImageUrlOrNull(),
                gallery.ItemCount);
    }

    extension(IGalleryQueries.ManagedGalleryListing gallery)
    {
        public ManagedGalleryItem ToItem(DeletionState state) =>
            new(
                gallery.Id,
                gallery.LatestVersion,
                gallery.ActiveVersion.ToNullable(),
                gallery.Title,
                gallery.Author,
                gallery.CreatedAt.ToNullable(),
                gallery.UpdatedAt.ToNullable(),
                gallery.ThumbnailId.ToImageUrlOrNull(),
                gallery.ItemCount,
                state == DeletionState.Deleted,
                gallery.ActiveVersion.Match(() => NewsResponseExtensions.DraftStatus, _ => ArticleResponseExtensions.PublishedStatus),
                gallery.ActiveVersion.Match(() => false, version => version < gallery.LatestVersion));
    }

    extension(IGalleryQueries.GalleryItemInfo item)
    {
        public GalleryImageItem ToItem() =>
            new(
                item.Id,
                item.Description,
                item.ImageId,
                item.ImageId.ToImageUrl(),
                item.ThumbnailId,
                item.ThumbnailId.ToImageUrl(),
                item.Position);
    }

    extension(IGalleryQueries.GalleryDetail gallery)
    {
        public GalleryDetailResponse ToResponse() =>
            new(
                gallery.Id.Key,
                gallery.Id.Version,
                gallery.Title,
                gallery.Description,
                gallery.Author,
                gallery.CreatedAt.ToNullable(),
                gallery.UpdatedAt.ToNullable(),
                gallery.ThumbnailId.ToNullable(),
                gallery.ThumbnailId.ToImageUrlOrNull(),
                gallery.ThumbnailGenerated,
                gallery.Items.Select(item => item.ToItem()).ToList());
    }

    extension(IGalleryQueries.GalleryHistory history)
    {
        public GalleryHistoryResponse ToResponse() =>
            new(
                history.Id,
                history.ActiveVersion.ToNullable(),
                history.Versions
                    .Select(version => new GalleryVersionItem(
                        version.Version,
                        version.Title,
                        version.ItemCount,
                        version.CreatedAt.ToNullable(),
                        version.IsActive))
                    .ToList());
    }

    extension(GallerySaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                GallerySaveResult.Saved saved => Results.Json(new NewsSavedResponse(saved.Id.Key, saved.Id.Version)),
                GallerySaveResult.NotFound => Results.NotFound(),
                GallerySaveResult.UnknownImage => Results.Conflict(
                    new NewsConflictResponse(NewsResponseExtensions.UnknownImageReason)),
                _ => Results.Conflict(new NewsConflictResponse(NewsResponseExtensions.VersionConflictReason))
            };
    }
}
