using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;

namespace Behind.Tests.Helpers;

public static class DownloadTestBedExtensions
{
    extension(NewsTestBed bed)
    {
        public async Task<TResult> WithDownloadFolderCommandsAsync<TResult>(
            Func<DownloadFolderCommands, Task<TResult>> action,
            DateTimeOffset? at = null)
        {
            await using var ctx = bed.Write(at);

            return await action(new DownloadFolderCommands(ctx, new VfsCommands(ctx, bed.ContentStore)));
        }

        public async Task<TResult> WithDownloadCommandsAsync<TResult>(
            Func<DownloadCommands, Task<TResult>> action,
            DateTimeOffset? at = null)
        {
            await using var ctx = bed.Write(at);

            return await action(new DownloadCommands(ctx, new VfsCommands(ctx, bed.ContentStore)));
        }

        public async Task<TResult> WithDownloadQueriesAsync<TResult>(Func<DownloadQueries, Task<TResult>> action)
        {
            await using var ctx = bed.Read();

            return await action(new DownloadQueries(ctx, new VfsQueries(ctx, bed.ContentStore)));
        }

        public async Task<Guid> CreateDownloadFolderAsync(
            string displayName = "Manuals",
            string slug = "manuals",
            Guid? parentId = null,
            DateTimeOffset? at = null)
        {
            var result = await bed.WithDownloadFolderCommandsAsync(
                commands => commands.Create(
                    parentId.ToOption(),
                    new IDownloadFolderCommands.FolderDraft(slug, displayName)),
                at);

            return Assert.IsType<DownloadFolderSaveResult.Saved>(result).Id.Key;
        }

        public async Task<Guid> UploadDownloadAsync(
            Guid folderId,
            string displayName = "Guide",
            string fileName = "guide.pdf",
            string contentType = "application/pdf",
            byte[]? content = null,
            Guid? uploadedById = null,
            DateTimeOffset? at = null)
        {
            var result = await bed.WithDownloadCommandsAsync(
                commands => commands.Add(
                    folderId,
                    uploadedById ?? bed.AuthorId,
                    new IDownloadCommands.FileDraft(displayName, fileName, contentType, content ?? [1, 2, 3])),
                at);

            return Assert.IsType<DownloadSaveResult.Saved>(result).Id.Key;
        }

        public async Task<bool> StoredAsync(string path) =>
            (await bed.ContentStore.Read(path)).Match(() => false, _ => true);
    }
}
