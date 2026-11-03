using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IDownloadCommands
{
    public sealed record FileDraft(string DisplayName, string FileName, string ContentType, byte[] Content);

    Task<DownloadSaveResult> Add(
        Guid folderId,
        Guid uploadedById,
        FileDraft draft,
        CancellationToken cancellationToken = default);

    Task<DownloadSaveResult> Rename(
        Guid downloadId,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid downloadId, CancellationToken cancellationToken = default);

    Task<DownloadSaveResult> Restore(Guid downloadId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid downloadId, CancellationToken cancellationToken = default);
}
