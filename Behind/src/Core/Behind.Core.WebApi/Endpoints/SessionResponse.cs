namespace Behind.Core.WebApi.Endpoints;

public sealed record SessionRole(string Name, IReadOnlyList<string> Grants);

public sealed record SessionUser(
    Guid Id,
    string DisplayName,
    IReadOnlyList<string> Emails,
    IReadOnlyList<SessionRole> Roles,
    IReadOnlyList<string> Grants);

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

public sealed record RefreshedTokens(string AccessToken, DateTimeOffset Expires, string RefreshToken);

public sealed record SessionImpersonator(string DisplayName, IReadOnlyList<string> Emails);

public sealed record SessionResponse(
    Guid SessionId,
    SessionUser User,
    SessionDetails Session,
    RefreshedTokens? Refreshed,
    SessionImpersonator? Impersonator = null);
