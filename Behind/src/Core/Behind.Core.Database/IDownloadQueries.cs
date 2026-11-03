using Behind.Common;

namespace Behind.Core.Database;

public interface IDownloadQueries
{
    public sealed record DownloadFolderInfo(Guid Id, Option<Guid> ParentId, string Slug, string DisplayName);

    public sealed record DownloadInfo(
        Guid Id,
        Guid FolderId,
        string DisplayName,
        string FileName,
        string ContentType,
        long Length,
        string UploadedBy,
        Option<DateTimeOffset> UploadedAt);

    public sealed record DeletedDownloads(
        IReadOnlyList<DownloadFolderInfo> Folders,
        IReadOnlyList<DownloadInfo> Files);

    Task<IReadOnlyList<DownloadFolderInfo>> ListFolders(CancellationToken cancellationToken = default);

    Task<Option<IReadOnlyList<DownloadInfo>>> ListFiles(
        Guid folderId,
        CancellationToken cancellationToken = default);

    Task<DeletedDownloads> ListDeleted(CancellationToken cancellationToken = default);

    Task<Option<IVfsQueries.Content>> Open(Guid downloadId, CancellationToken cancellationToken = default);
}
