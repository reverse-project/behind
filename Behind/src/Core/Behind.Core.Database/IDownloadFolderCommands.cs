using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IDownloadFolderCommands
{
    public sealed record FolderDraft(string Slug, string DisplayName);

    Task<DownloadFolderSaveResult> Create(
        Option<Guid> parentId,
        FolderDraft draft,
        CancellationToken cancellationToken = default);

    Task<DownloadFolderSaveResult> Update(
        Guid folderId,
        FolderDraft draft,
        CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid folderId, CancellationToken cancellationToken = default);

    Task<DownloadFolderSaveResult> Restore(Guid folderId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid folderId, CancellationToken cancellationToken = default);
}
