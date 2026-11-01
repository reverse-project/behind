using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public static class AccessPolicyExtensions
{
    extension(UserManagementActor actor)
    {
        public bool CanGrant(IEnumerable<Grant> grants)
        {
            var held = actor.Access.CalculatedGrants;

            return actor.IsDeveloper || grants.All(held.Contains);
        }

        public bool CanEditRole(Role role) => actor.CanGrant(role.EffectiveGrants);

        public bool CanChangeRole(Role role, AccessSelection selection) =>
            actor.CanEditRole(role) && actor.CanGrant(role.GrantsAddedBy(role.With(selection)));

        public bool CanDeleteRole(Guid roleId, Role role) =>
            !BuiltInAccess.IsBuiltIn(roleId) && actor.CanEditRole(role);

        public bool CanChangeAccess(UserAccess current, UserAccess proposed) =>
            actor.CanGrant(current.GrantsAddedBy(proposed));
    }
}
