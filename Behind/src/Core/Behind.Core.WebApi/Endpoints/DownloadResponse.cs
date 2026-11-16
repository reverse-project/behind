namespace Behind.Core.WebApi.Endpoints;

public sealed record DownloadFolderItem(Guid Id, Guid? ParentId, string Slug, string DisplayName);

public sealed record DownloadFileItem(
    Guid Id,
    Guid FolderId,
    string DisplayName,
    string FileName,
    string ContentType,
    long Length,
    string UploadedBy,
    DateTimeOffset? UploadedAt,
    string Url)
{
    public Guid UploadedById { get; init; }
}

public sealed record DownloadFoldersResponse(long MaxFileBytes, IReadOnlyList<DownloadFolderItem> Folders);

public sealed record DownloadFilesResponse(IReadOnlyList<DownloadFileItem> Files);

public sealed record DownloadDeletedResponse(
    IReadOnlyList<DownloadFolderItem> Folders,
    IReadOnlyList<DownloadFileItem> Files);

public sealed record DownloadUploadedFile(string FileName, Guid? Id, string? Error);

public sealed record DownloadUploadResponse(IReadOnlyList<DownloadUploadedFile> Files);

public sealed record DownloadLimitResponse(long MaxBytes);
