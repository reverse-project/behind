using Behind.Common;
using Behind.Core.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class GalleryThumbnails(IVfsQueries images, IVfsCommands commands)
{
    public const int MosaicSide = 600;
    public const string MosaicFileName = "gallery-thumbnail.webp";
    public const int MosaicItems = 4;

    public Task<IGalleryCommands.GalleryDraft> Complete(
        IGalleryCommands.GalleryDraft draft,
        CancellationToken cancellationToken = default) =>
        draft.ThumbnailImageId.Match(
            () => Generate(draft, cancellationToken),
            _ => Task.FromResult(draft));

    private async Task<IGalleryCommands.GalleryDraft> Generate(
        IGalleryCommands.GalleryDraft draft,
        CancellationToken cancellationToken)
    {
        var contents = new List<byte[]>();

        foreach (var item in draft.Items.Take(MosaicItems))
        {
            var image = await images.Open(VfsTypes.GalleryImages, item.ThumbnailId, cancellationToken);

            contents.AddRange(image.Map(found => found.Bytes).AsEnumerable());
        }

        return await contents.ToMosaic(MosaicSide).Match(
            () => Task.FromResult(draft),
            async mosaic =>
            {
                var id = await commands.Add(
                    new IVfsCommands.Upload(VfsTypes.Galleries, MosaicFileName, mosaic.ContentType, mosaic.Content),
                    cancellationToken);

                return draft with { ThumbnailImageId = Some(id) };
            });
    }
}
