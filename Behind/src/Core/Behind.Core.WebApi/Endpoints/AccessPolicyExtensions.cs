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
            actor.Access.Roles.Any(held => string.Equals(held.Name, role.Name, StringComparison.Ordinal));

        public bool CanChangeRole(Role role, AccessSelection selection)
        {
            var proposed = role.With(selection);
            var added = role.GrantsAddedBy(proposed)
                .Concat(role.Restricted.Except(proposed.Restricted).Select(value => new Grant(value)))
                .Concat(proposed.Granted.Except(role.Granted))
                .Distinct().ToList();

            return actor.CanEditRole(role)
                   && actor.CanGrant(added)
                   && actor.CanGrant(proposed.Restricted.Except(role.Restricted).Select(value => new Grant(value)))
                   && (added.Count == 0 || !actor.HoldsRole(role));
        }

        public bool CanResetRole(Role role, AccessSelection defaults) =>
            actor.IsDeveloper
            || (!role.IsDeveloperRole
                && actor.CanGrant(role.EffectiveGrants)
                && actor.CanGrant(role.GrantsAddedBy(role.With(defaults)))
                && actor.CanGrant(role.Restricted.Except(role.With(defaults).Restricted).Select(value => new Grant(value))));

        public bool CanDeleteRole(Guid roleId, Role role) =>
            !actor.Catalog.IsBuiltIn(roleId)
            && actor.CanEditRole(role)
            && actor.CanGrant(role.Restricted.Select(value => new Grant(value)))
            && (!actor.HoldsRole(role) || role.Restricted.Count == 0);

        public bool CanChangeAccess(UserAccess current, UserAccess proposed) =>
            actor.CanGrant(proposed.Grants.Except(current.Grants))
            && actor.CanGrant(current.GrantsAddedBy(proposed))
            && (actor.IsDeveloper || current.DeveloperRolesAddedBy(proposed).Count == 0);

        public bool CanSetDeveloperFlag(Guid roleId) =>
            actor.IsDeveloper && !actor.Catalog.IsBuiltIn(roleId);
    }
}
