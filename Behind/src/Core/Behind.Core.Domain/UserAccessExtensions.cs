namespace Behind.Core.Domain;

public static class UserAccessExtensions
{
    extension(UserAccess access)
    {
        public IReadOnlyList<Grant> CalculatedGrants =>
            access.Roles
                .SelectMany(role => role.Granted)
                .Concat(access.Grants)
                .Except(access.Roles.SelectMany(role => role.Restricted).Concat(access.Restrictions).Select(restriction => new Grant(restriction)))
                .Distinct()
                .OrderBy(grant => grant.Name, StringComparer.Ordinal)
                .ToList();

        public bool Holds(string grantName) => access.CalculatedGrants.Contains(new Grant(grantName));

        public bool IsDeveloper => access.Holds(ManagedRole.Developer.RequiredGrant);

        public IReadOnlyList<Grant> GrantsAddedBy(UserAccess proposed) =>
            proposed.CalculatedGrants.Except(access.CalculatedGrants).ToList();

        public IReadOnlyList<Role> DeveloperRolesAddedBy(UserAccess proposed) =>
            proposed.Roles
                .Where(role => role.IsDeveloperRole)
                .ExceptBy(access.Roles.Select(held => held.Name), role => role.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        public UserAccess With(IReadOnlyList<Role> roles, AccessSelection selection) =>
            access with
            {
                Roles = roles,
                Grants = selection.Grants.Select(name => (Grant)name).ToList(),
                Restrictions = selection.Restrictions.Select(name => (Restriction)name).ToList()
            };
    }
}
