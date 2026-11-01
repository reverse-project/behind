namespace Behind.Core.WebApi.Endpoints;

public sealed record SessionListItem(
    Guid Id,
    bool IsCurrent,
    DateTimeOffset StartedAt,
    DateTimeOffset LastActiveAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? EndedAt,
    bool SignedOut,
    string RemoteAddress,
    string UserAgent);

public sealed record SessionPage(
    long Total,
    long Page,
    long PageCount,
    int PageSize,
    IReadOnlyList<SessionListItem> Items);

public sealed record SessionListResponse(Guid CurrentSessionId, SessionPage Active, SessionPage Ended);
