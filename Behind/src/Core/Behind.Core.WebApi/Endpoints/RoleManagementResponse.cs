namespace Behind.Core.WebApi.Endpoints;

public sealed record RoleItem(
    Guid Id,
    string Name,
    bool IsBuiltIn,
    IReadOnlyList<string> Grants,
    IReadOnlyList<string> Restrictions,
    bool CanEdit,
    bool CanDelete);

public sealed record RoleListResponse(IReadOnlyList<RoleItem> Roles, IReadOnlyList<string> AvailableGrants);

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
