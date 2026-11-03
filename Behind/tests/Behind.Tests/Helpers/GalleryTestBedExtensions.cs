using Behind.Core.Database;
using Behind.Core.Database.Commands;

namespace Behind.Tests.Helpers;

public static class GalleryTestBedExtensions
{
    extension(NewsTestBed bed)
    {
        public async Task<TResult> WithGalleryCommandsAsync<TResult>(
            Func<GalleryCommands, Task<TResult>> action,
            DateTimeOffset? at = null)
        {
            await using var ctx = bed.Write(at);
            await using var versions = bed.WriteVersions(at);

            return await action(new GalleryCommands(ctx, versions, new VfsCommands(ctx, bed.ContentStore)));
        }

        public Task<Guid> AddGalleryImageAsync(string fileName = "photo.png") =>
            bed.AddVfsFileAsync(VfsTypes.Galleries, fileName);

        public async Task<IGalleryCommands.GalleryItemDraft> NewGalleryItemAsync(string description = "A photo") =>
            new(await bed.AddGalleryImageAsync(), await bed.AddGalleryImageAsync(), description);

        public async Task<IReadOnlyList<IGalleryCommands.GalleryItemDraft>> NewGalleryItemsAsync(
            params string[] descriptions)
        {
            var items = new List<IGalleryCommands.GalleryItemDraft>();

            foreach (var description in descriptions)
            {
                items.Add(await bed.NewGalleryItemAsync(description));
            }

            return items;
        }

        public async Task<VersionedKey<Guid>> SaveGalleryAsync(
            Guid? key,
            string title = "Holiday",
            string description = "Summer",
            IReadOnlyList<IGalleryCommands.GalleryItemDraft>? items = null,
            Option<Guid> thumbnailImageId = default,
            DateTimeOffset? at = null,
            Guid? authorId = null,
            bool publish = true)
        {
            var draft = new IGalleryCommands.GalleryDraft(
                title,
                description,
                thumbnailImageId,
                !thumbnailImageId.Match(() => false, _ => true),
                items ?? await bed.NewGalleryItemsAsync("First", "Second"));

            var result = await bed.WithGalleryCommandsAsync(
                commands => key is { } existing
                    ? commands.SaveVersion(existing, draft, publish)
                    : commands.Create(authorId ?? bed.AuthorId, draft, publish),
                at);

            return Assert.IsType<GallerySaveResult.Saved>(result).Id;
        }

        public async Task<Guid> CreateGalleryAsync(
            string title = "Holiday",
            string description = "Summer",
            IReadOnlyList<IGalleryCommands.GalleryItemDraft>? items = null,
            Option<Guid> thumbnailImageId = default,
            DateTimeOffset? at = null,
            Guid? authorId = null,
            bool publish = true) =>
            (await bed.SaveGalleryAsync(null, title, description, items, thumbnailImageId, at, authorId, publish)).Key;
    }
}
