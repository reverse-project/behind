using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using SkiaSharp;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class GalleryThumbnailsTests
{
    private static readonly Guid MosaicId = Guid.NewGuid();

    private static IGalleryCommands.GalleryDraft Draft(
        Option<Guid> thumbnail,
        params IGalleryCommands.GalleryItemDraft[] items) =>
        new("Trip", string.Empty, thumbnail, !thumbnail.Match(() => false, _ => true), items);

    private static (GalleryThumbnails Thumbnails, IVfsCommands Commands, List<IVfsCommands.Upload> Stored) Create(
        IReadOnlyDictionary<Guid, byte[]> images)
    {
        var queries = Substitute.For<IVfsQueries>();
        queries.Open(VfsTypes.Galleries, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call =>
            images.TryGetValue(call.ArgAt<Guid>(1), out var content)
                ? Some(new IVfsQueries.Content(new VfsFile(VfsTypes.Galleries, "t.png", "image/png", content.Length), content))
                : None);

        var stored = new List<IVfsCommands.Upload>();
        var commands = Substitute.For<IVfsCommands>();
        commands.Add(Arg.Any<IVfsCommands.Upload>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            stored.Add(call.Arg<IVfsCommands.Upload>());

            return MosaicId;
        });

        return (new GalleryThumbnails(queries, commands), commands, stored);
    }

    [Fact]
    public async Task Complete_KeepsAnUploadedThumbnailAndStoresNothing()
    {
        var uploaded = Guid.NewGuid();
        var (thumbnails, commands, _) = Create(new Dictionary<Guid, byte[]>());
        var draft = Draft(Some(uploaded), new IGalleryCommands.GalleryItemDraft(Guid.NewGuid(), Guid.NewGuid(), "a"));

        var completed = await thumbnails.Complete(draft);

        Assert.Equal(draft, completed);
        await commands.DidNotReceiveWithAnyArgs().Add(default!, default);
    }

    [Fact]
    public async Task Complete_ComposesAMosaicOfTheFirstFourItemThumbnails()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var images = ids.ToDictionary(id => id, _ => TestImages.Png(120, 80));
        var (thumbnails, _, stored) = Create(images);
        var items = ids.Select(id => new IGalleryCommands.GalleryItemDraft(Guid.NewGuid(), id, "a")).ToArray();

        var completed = await thumbnails.Complete(Draft(None, items));

        Assert.Equal((Some(MosaicId), true), (completed.ThumbnailImageId, completed.ThumbnailGenerated));
        var image = Assert.Single(stored);
        Assert.Equal(
            (VfsTypes.Galleries, GalleryThumbnails.MosaicFileName, "image/webp"),
            (image.Type, image.FileName, image.ContentType));
        Assert.Equal((GalleryThumbnails.MosaicSide, GalleryThumbnails.MosaicSide), TestImages.SizeOf(image.Content));
    }

    [Fact]
    public async Task Complete_WithoutUsableItemThumbnails_LeavesTheThumbnailEmptyButGenerated()
    {
        var (thumbnails, commands, _) = Create(new Dictionary<Guid, byte[]>());
        var draft = Draft(None, new IGalleryCommands.GalleryItemDraft(Guid.NewGuid(), Guid.NewGuid(), "a"));

        var completed = await thumbnails.Complete(draft);

        Assert.Equal((None, true), (completed.ThumbnailImageId, completed.ThumbnailGenerated));
        await commands.DidNotReceiveWithAnyArgs().Add(default!, default);
    }

    [Fact]
    public async Task Complete_AnEmptyGallery_StoresNoThumbnail()
    {
        var (thumbnails, commands, _) = Create(new Dictionary<Guid, byte[]>());

        var completed = await thumbnails.Complete(Draft(None));

        Assert.Equal(None, completed.ThumbnailImageId);
        await commands.DidNotReceiveWithAnyArgs().Add(default!, default);
    }
}
