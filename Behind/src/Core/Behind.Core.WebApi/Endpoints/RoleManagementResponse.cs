using Behind.Common.WebApi;

namespace Behind.Core.WebApi.Endpoints;

public sealed record RoleItem(
    Guid Id,
    string Name,
    bool IsBuiltIn,
    IReadOnlyList<string> Grants,
    IReadOnlyList<string> Restrictions,
    bool CanEdit,
    bool CanDelete,
    bool IsDeleted = false,
    bool IsDeveloper = false,
    bool CanSetDeveloper = false);

public sealed record RoleListResponse(
    PageResponse<RoleItem> Roles,
    IReadOnlyList<string> AvailableGrants,
    IReadOnlyList<string> Letters);

public sealed record AccessRoleItem(
    Guid Id,
    string Name,
    IReadOnlyList<string> Grants,
    IReadOnlyList<string> Restrictions,
    bool IsRequired);

public sealed record UserAccessResponse(
    Guid UserId,
    bool CanEdit,
    IReadOnlyList<AccessRoleItem> Roles,
    IReadOnlyList<AccessRoleItem> AvailableRoles,
    IReadOnlyList<string> Grants,
    IReadOnlyList<string> Restrictions,
    IReadOnlyList<string> CalculatedGrants,
    IReadOnlyList<string> AvailableGrants);
