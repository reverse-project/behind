using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface INewsCommands
{
    public sealed record NewsDraft(
        string Title,
        string Summary,
        string Content,
        Option<Guid> CategoryId,
        bool Highlighted = false,
        Option<DateTimeOffset> StartAt = default,
        Option<DateTimeOffset> EndAt = default);

    Task<NewsSaveResult> Create(
        Guid authorId,
        NewsDraft draft,
        bool publish,
        CancellationToken cancellationToken = default);

    Task<NewsSaveResult> SaveVersion(
        Guid newsId,
        NewsDraft draft,
        bool publish,
        CancellationToken cancellationToken = default);

    Task<NewsSaveResult> Activate(VersionedKey<Guid> id, CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid newsId, CancellationToken cancellationToken = default);

    Task<bool> Restore(Guid newsId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid newsId, CancellationToken cancellationToken = default);
}
