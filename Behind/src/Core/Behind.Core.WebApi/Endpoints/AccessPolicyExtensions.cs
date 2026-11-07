using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public static class AccessPolicyExtensions
{
    extension(UserManagementActor actor)
    {
        public bool CanGrant(IEnumerable<Grant> grants)
        {
            var held = actor.Access.CalculatedGrants;

            return actor.IsDeveloper
                   || grants.All(grant => held.Contains(grant) && !actor.Catalog.IsDeveloperGrant(grant.Name));
        }

        public bool CanEditRole(Role role) =>
            (actor.IsDeveloper || !role.IsDeveloperRole) && actor.CanGrant(role.EffectiveGrants);

        public bool HoldsRole(Role role) =>
            actor.Access.Roles.Any(held => string.Equals(held.Name, role.Name, StringComparison.OrdinalIgnoreCase));

        public bool CanChangeRole(Role role, AccessSelection selection)
        {
            var added = role.GrantsAddedBy(role.With(selection));

            return actor.CanEditRole(role)
                   && actor.CanGrant(added)
                   && (added.Count == 0 || !actor.HoldsRole(role));
        }

        public bool CanResetRole(Role role, AccessSelection defaults) =>
            actor.IsDeveloper
            || actor.HoldsRole(BuiltInAccess.Developers.Role)
            || (!role.IsDeveloperRole
                && actor.CanGrant(role.EffectiveGrants)
                && actor.CanGrant(role.GrantsAddedBy(role.With(defaults))));

        public bool CanDeleteRole(Guid roleId, Role role) =>
            !actor.Catalog.IsBuiltIn(roleId) && actor.CanEditRole(role);

        public bool CanChangeAccess(UserAccess current, UserAccess proposed) =>
            actor.CanGrant(current.GrantsAddedBy(proposed))
            && (actor.IsDeveloper || current.DeveloperRolesAddedBy(proposed).Count == 0);

        public bool CanSetDeveloperFlag(Guid roleId) =>
            actor.IsDeveloper && !actor.Catalog.IsBuiltIn(roleId);
    }
}
