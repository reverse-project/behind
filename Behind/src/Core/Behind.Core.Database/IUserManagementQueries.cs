using Behind.Common;

namespace Behind.Core.Database;

public interface IUserManagementQueries
{
    public sealed record ManagedUser(
        Guid Id,
        string DisplayName,
        IReadOnlyList<string> Emails,
        IReadOnlyList<string> Roles,
        Option<IWallOfShameQueries.ActiveBan> Ban);

    Task<PagedResult<ManagedUser>> ListUsers(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(Option<string> search, CancellationToken cancellationToken = default);

    Task<Option<ManagedUser>> TryFindUser(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default);
}
