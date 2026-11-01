namespace Behind.Core.WebApi.Endpoints;

public sealed record BanInfo(string Reason, DateTimeOffset? ExpiresAt);

public sealed record ManagedUserPermissions(
    bool CanChangeAdministrator,
    bool CanChangeDeveloper,
    bool CanModerate);

public sealed record ManagedUserItem(
    Guid Id,
    string DisplayName,
    IReadOnlyList<string> Emails,
    IReadOnlyList<string> Roles,
    bool IsAdministrator,
    bool IsDeveloper,
    bool IsCurrent,
    BanInfo? Ban,
    ManagedUserPermissions Permissions);

public sealed record WallOfShameItem(string DisplayName, string Reason, DateTimeOffset? ExpiresAt);
