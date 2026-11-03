using Behind.Common;
using Behind.Core.Database;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class DownloadResponseExtensions
{
    public const string FilePath = "/downloads/files";
    public const string SlugTakenReason = "slug-taken";
    public const string ParentDeletedReason = "parent-deleted";
    public const string FolderDeletedReason = "folder-deleted";
    public const string EmptyFileError = "empty";
    public const string TooLargeFileError = "too-large";
    public const string FolderNotFoundError = "folder-not-found";

    extension(IDownloadQueries.DownloadFolderInfo folder)
    {
        public DownloadFolderItem ToItem() =>
            new(folder.Id, folder.ParentId.ToNullable(), folder.Slug, folder.DisplayName);
    }

    extension(IDownloadQueries.DownloadInfo download)
    {
        public DownloadFileItem ToItem() =>
            new(
                download.Id,
                download.FolderId,
                download.DisplayName,
                download.FileName,
                download.ContentType,
                download.Length,
                download.UploadedBy,
                download.UploadedAt.ToNullable(),
                $"{FilePath}/{download.Id}");
    }

    extension(IDownloadQueries.DeletedDownloads deleted)
    {
        public DownloadDeletedResponse ToResponse() =>
            new(deleted.Folders.Select(folder => folder.ToItem()).ToList(), deleted.Files.Select(file => file.ToItem()).ToList());
    }

    extension(DownloadFolderSaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                DownloadFolderSaveResult.Saved saved => Results.Json(new NewsSavedResponse(saved.Id.Key, saved.Id.Version)),
                DownloadFolderSaveResult.NotFound or DownloadFolderSaveResult.ParentNotFound => Results.NotFound(),
                DownloadFolderSaveResult.SlugTaken => Conflict(SlugTakenReason),
                DownloadFolderSaveResult.ParentDeleted => Conflict(ParentDeletedReason),
                _ => Conflict(NewsResponseExtensions.VersionConflictReason)
            };

        public IResult ToRestoreResult() =>
            result is DownloadFolderSaveResult.Saved ? Results.NoContent() : result.ToResult();
    }

    extension(DownloadSaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                DownloadSaveResult.Saved saved => Results.Json(new NewsSavedResponse(saved.Id.Key, saved.Id.Version)),
                DownloadSaveResult.NotFound or DownloadSaveResult.FolderNotFound => Results.NotFound(),
                DownloadSaveResult.FolderDeleted => Conflict(FolderDeletedReason),
                _ => Conflict(NewsResponseExtensions.VersionConflictReason)
            };

        public IResult ToRestoreResult() =>
            result is DownloadSaveResult.Saved ? Results.NoContent() : result.ToResult();
    }

    private static IResult Conflict(string reason) => Results.Conflict(new NewsConflictResponse(reason));
}
