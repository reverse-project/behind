using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IUserManagementQueries
{
    public sealed record ManagedUser(
        Guid Id,
        string DisplayName,
        IReadOnlyList<string> Emails,
        IReadOnlyList<string> Roles,
        Option<IWallOfShameQueries.ActiveBan> Ban,
        DeletionState State = DeletionState.Active,
        bool EmailConfirmed = true);

    Task<PagedResult<ManagedUser>> ListUsers(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<Option<ManagedUser>> TryFindUser(
        Guid id,
        DeletionState state,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
