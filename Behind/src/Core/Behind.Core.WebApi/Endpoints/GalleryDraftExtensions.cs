using Behind.Common;
using Behind.Core.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public static class GalleryDraftExtensions
{
    extension(GalleryEndpoints.SaveGalleryRequest request)
    {
        public Option<IGalleryCommands.GalleryDraft> ToDraft()
        {
            var title = (request.Title ?? string.Empty).Trim();
            var items = (request.Items ?? []).Select(item => item.ToDraft()).ToList();

            return title.Length == 0 || items.Count > Gallery.MaxItems || items.Any(item => item.IsNone())
                ? None
                : Some(new IGalleryCommands.GalleryDraft(
                    title,
                    (request.Description ?? string.Empty).Trim(),
                    request.ThumbnailImageId.ToOption(),
                    request.ThumbnailImageId is null,
                    items.SelectMany(item => item.AsEnumerable()).ToList()));
        }
    }

    extension(GalleryEndpoints.SaveGalleryItemRequest item)
    {
        private Option<IGalleryCommands.GalleryItemDraft> ToDraft()
        {
            var description = (item.Description ?? string.Empty).Trim();

            return item.ImageId == Guid.Empty
                   || item.ThumbnailId == Guid.Empty
                   || description.Length > GalleryItem.MaxDescriptionLength
                ? None
                : Some(new IGalleryCommands.GalleryItemDraft(item.ImageId, item.ThumbnailId, description));
        }
    }
}
