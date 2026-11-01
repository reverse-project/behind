using Behind.Core.Database;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public static class RoleManagementResponseExtensions
{
    extension(IRoleQueries.RoleResult result)
    {
        public RoleItem ToItem(UserManagementActor actor) =>
            new(
                result.Id,
                result.Role.Name,
                BuiltInAccess.IsBuiltIn(result.Id),
                result.Role.GrantNames,
                result.Role.RestrictionNames,
                actor.CanEditRole(result.Role),
                actor.CanDeleteRole(result.Id, result.Role));

        public AccessRoleItem ToAccessItem() =>
            new(
                result.Id,
                result.Role.Name,
                result.Role.GrantNames,
                result.Role.RestrictionNames,
                result.Id == BuiltInAccess.Users.Id);
    }

    extension(UserAccess access)
    {
        public UserAccessResponse ToResponse(
            Guid userId,
            bool canEdit,
            IReadOnlyList<IRoleQueries.RoleResult> allRoles)
        {
            var assigned = access.Roles.Select(role => role.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            return new UserAccessResponse(
                userId,
                canEdit,
                allRoles.Where(role => assigned.Contains(role.Role.Name)).Select(role => role.ToAccessItem()).ToList(),
                allRoles.Select(role => role.ToAccessItem()).ToList(),
                access.Grants.Names,
                access.Restrictions.Names,
                access.CalculatedGrants.Names,
                BuiltInAccess.GrantNames);
        }
    }
}
