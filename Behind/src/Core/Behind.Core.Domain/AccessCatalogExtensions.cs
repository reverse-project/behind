using Behind.Common.Modules;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public static class AccessCatalogExtensions
{
    extension(ModuleCatalog modules)
    {
        internal IEnumerable<string> Conflicts =>
        [
            ..modules.GrantAreas
                .Select(area => area.Area)
                .Where(area => GrantArea.All.Contains(area) || area == GrantArea.RawDatabase)
                .Select(area => $"Grant area '{area}' is already used by the Core module"),
            ..modules.MenuItems
                .Where(item => BuiltInMenus.Main.Items.Any(builtIn => builtIn.Id == item.Id))
                .Select(item => $"Menu item id '{item.Id}' is already used by the Core module")
        ];

        internal IEnumerable<string> UnknownAdministrationGrants
        {
            get
            {
                var known = modules.GrantAreas
                    .SelectMany(area => area.Actions.Select(action => GrantName.Of(area.Area, action)))
                    .Concat(BuiltInAccess.GrantNames)
                    .ToHashSet(StringComparer.Ordinal);

                return modules.AdministrationPages
                    .Where(page => !known.Contains(page.Grant))
                    .Select(page => $"Administration page '{page.Name}' needs the unknown grant '{page.Grant}'");
            }
        }
    }

    extension(BuiltInRole role)
    {
        internal BuiltInRole Including(IReadOnlyList<BuiltInGrant> added) =>
            added.Count == 0
                ? role
                : new BuiltInRole(
                    role.Id,
                    role.Role with
                    {
                        Grants = Some<IEnumerable<Grant>>([..role.Grants.Concat(added).Select(grant => grant.Grant)])
                    })
                {
                    Grants = [..role.Grants, ..added]
                };
    }
}
