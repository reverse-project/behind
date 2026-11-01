using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface ISessionQueries
{
    public sealed record SessionSummary(
        Guid Id,
        DateTimeOffset StartedAt,
        DateTimeOffset LastActiveAt,
        DateTimeOffset ExpiresAt,
        RemoteAddress RemoteAddress,
        UserAgent UserAgent,
        bool SignedOut,
        Option<DateTimeOffset> SignedOutAt);

    public sealed record SessionActor(Guid UserId, Option<Guid> ImpersonatorId);

    public sealed record UserSessions(PagedResult<SessionSummary> Active, PagedResult<SessionSummary> Ended);

    Task<Option<Session>> TryFindById(Guid id, CancellationToken cancellationToken = default);
    Task<Option<(Session Session, Guid UserId)>> TryFindByIdWithUserId(Guid id, CancellationToken cancellationToken = default);
    Task<Option<SessionActor>> TryFindActor(Guid id, CancellationToken cancellationToken = default);

    Task<UserSessions> GetUserSessions(
        Guid userId,
        DateTimeOffset now,
        Flipper active,
        Flipper ended,
        CancellationToken cancellationToken = default);
}
