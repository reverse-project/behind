using Behind.Common.WebApi;

namespace Behind.Core.WebApi.Endpoints;

public sealed record RestrictionStatusResponse(bool Restricted, string Message, string Title = "");

public sealed record AccessSettingsResponse(
    bool RestrictionEnabled,
    string RestrictedToRole,
    string RestrictionMessage,
    bool ModeratedSignUp,
    bool IsDeleted,
    string RestrictionTitle = "");

public sealed record AccessManageResponse(AccessSettingsResponse Settings, IReadOnlyList<string> Roles);

public sealed record PendingUserResponse(Guid Id, string DisplayName, IReadOnlyList<string> Emails);

public sealed record PendingUsersResponse(PageResponse<PendingUserResponse> Users);
