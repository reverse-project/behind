using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public static class RoleExtensions
{
    extension(Role role)
    {
        public IReadOnlyList<Grant> Granted =>
            role.Grants.Match(
                () => (IReadOnlyList<Grant>)[],
                grants => grants.ToList());

        public IReadOnlyList<Restriction> Restricted =>
            role.Restrictions.Match(
                () => (IReadOnlyList<Restriction>)[],
                restrictions => restrictions.ToList());

        public IReadOnlyList<string> GrantNames => role.Granted.Names;

        public IReadOnlyList<string> RestrictionNames => role.Restricted.Names;

        public IReadOnlyList<Grant> EffectiveGrants =>
            role.Granted.Except(role.Restricted.Select(restriction => new Grant(restriction))).ToList();

        public bool IsDeveloperRole => role is UserRole { IsDeveloper: true };

        public bool Holds(string grantName) => role.EffectiveGrants.Contains(new Grant(grantName));

        public Role With(AccessSelection selection) =>
            role with
            {
                Grants = Some<IEnumerable<Grant>>(selection.Grants.Select(name => (Grant)name).ToList()),
                Restrictions = Some<IEnumerable<Restriction>>(
                    selection.Restrictions.Select(name => (Restriction)name).ToList())
            };

        public IReadOnlyList<Grant> GrantsAddedBy(Role proposed) =>
            proposed.EffectiveGrants.Except(role.EffectiveGrants).ToList();
    }
}
