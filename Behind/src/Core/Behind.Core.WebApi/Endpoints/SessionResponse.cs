namespace Behind.Core.WebApi.Endpoints;

public sealed record SessionRole(string Name, IReadOnlyList<string> Grants);

public sealed record SessionUser(
    Guid Id,
    string DisplayName,
    IReadOnlyList<string> Emails,
    IReadOnlyList<SessionRole> Roles);

public sealed record SessionDetails(
    DateTimeOffset StartedAt,
    TimeSpan Length,
    DateTimeOffset AccessTokenIssuedAt,
    DateTimeOffset AccessTokenExpiresAt,
    TimeSpan AccessTokenRemaining,
    DateTimeOffset ExpiresAt,
    TimeSpan Remaining,
    string RemoteAddress,
    string UserAgent);

public sealed record RefreshedAccessToken(string AccessToken, DateTimeOffset Expires);

public sealed record SessionResponse(
    Guid SessionId,
    SessionUser User,
    SessionDetails Session,
    RefreshedAccessToken? Refreshed);
