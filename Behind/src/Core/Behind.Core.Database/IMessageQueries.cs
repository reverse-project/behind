using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IMessageQueries
{
    public sealed record MessageListing(
        VersionedKey<Guid> Id,
        string Name,
        string Title,
        MessageLevel Level,
        DateTimeOffset? StartsAt,
        DateTimeOffset? EndsAt);

    public sealed record MessageDetails(
        VersionedKey<Guid> Id,
        string Name,
        string Title,
        string Content,
        MessageLevel Level,
        DateTimeOffset? StartsAt,
        DateTimeOffset? EndsAt);

    Task<PagedResult<MessageListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<Option<MessageDetails>> Find(Guid key, DeletionState state, CancellationToken cancellationToken = default);

    Task<Option<MessageDetails>> FindLive(Guid key, DateTimeOffset now, CancellationToken cancellationToken = default);
}
