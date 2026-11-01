using Behind.Common;

namespace Behind.Core.Domain;

public static class RoleExtensions
{
    extension(Role role)
    {
        public bool Holds(string grantName) =>
            role.Grants.Match(
                () => false,
                grants => grants.Any(grant => grant.Name == grantName));
    }
}
