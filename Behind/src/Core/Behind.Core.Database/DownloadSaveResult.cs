using Behind.Common;

namespace Behind.Core.Database;

public abstract record DownloadFolderSaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : DownloadFolderSaveResult;

    public sealed record NotFound : DownloadFolderSaveResult;

    public sealed record ParentNotFound : DownloadFolderSaveResult;

    public sealed record ParentDeleted : DownloadFolderSaveResult;

    public sealed record SlugTaken : DownloadFolderSaveResult;

    public sealed record VersionConflict : DownloadFolderSaveResult;
}

public abstract record DownloadSaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : DownloadSaveResult;

    public sealed record NotFound : DownloadSaveResult;

    public sealed record FolderNotFound : DownloadSaveResult;

    public sealed record FolderDeleted : DownloadSaveResult;

    public sealed record VersionConflict : DownloadSaveResult;
}
