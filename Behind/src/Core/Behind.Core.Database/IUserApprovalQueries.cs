using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IUserApprovalQueries
{
    public sealed record PendingUser(Guid Id, string DisplayName, IReadOnlyList<string> Emails);

    Task<bool> IsPending(Guid userId, CancellationToken cancellationToken = default);

    Task<PagedResult<PendingUser>> ListPending(
        Flipper flipper,
        Option<string> search,
        CancellationToken cancellationToken = default);
}
