using Behind.Common;
using Behind.Common.Modules;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public sealed record AccessCatalog(
    IReadOnlyList<BuiltInGrant> Grants,
    IReadOnlyList<BuiltInRole> Roles,
    IReadOnlyList<string> DeveloperGrantNames)
{
    private const string ModuleGrantPrefix = "61000000";

    public static AccessCatalog Core { get; } = new(
        BuiltInAccess.Grants,
        BuiltInAccess.Roles,
        BuiltInAccess.DeveloperGrantNames);

    public static Validation<AccessCatalog> From(ModuleCatalog modules)
    {
        var problems = modules.Conflicts.Concat(modules.UnknownAdministrationGrants).ToList();

        return problems.Count == 0
            ? Valid(Extend(modules))
            : Invalid<AccessCatalog>(problems.Select(problem => Error(problem)));
    }

    public IReadOnlyList<BuiltInRestriction> Restrictions => [..Grants.Select(BuiltInAccess.RestrictionOf)];

    public IReadOnlyList<string> GrantNames => [..Grants.Select(grant => grant.Grant.Name)];

    public bool IsKnownGrant(string name) => Grants.Any(grant => grant.Grant.Name == name);

    public bool IsDeveloperGrant(string name) => DeveloperGrantNames.Contains(name);

    public bool GrantsDeveloperAccess(Role role) => role.GrantNames.Any(IsDeveloperGrant);

    public bool GrantsDeveloperAccess(AccessSelection selection) => selection.Grants.Any(IsDeveloperGrant);

    public bool IsValid(AccessSelection selection) =>
        selection.Grants.Concat(selection.Restrictions).All(IsKnownGrant)
        && !selection.Grants.Intersect(selection.Restrictions, StringComparer.Ordinal).Any();

    public bool IsBuiltIn(Guid roleId) => Roles.Any(role => role.Id == roleId);

    public Option<BuiltInRole> FindRole(Guid roleId) => Roles.Find(role => role.Id == roleId);

    private static AccessCatalog Extend(ModuleCatalog modules)
    {
        var granted = modules.GrantAreas
            .SelectMany(area => area.Actions.Select(action => new ModuleGrant(area.Audience, ToGrant(area.Area, action))))
            .ToList();

        return new AccessCatalog(
            [..BuiltInAccess.Grants, ..granted.Select(grant => grant.Grant)],
            [..BuiltInAccess.Roles.Select(role => role.Including(granted.Where(grant => grant.IsFor(role)).Select(grant => grant.Grant).ToList()))],
            [..BuiltInAccess.DeveloperGrantNames, ..granted.Where(grant => grant.Audience == ModuleAudience.Developer).Select(grant => grant.Grant.Grant.Name)]);
    }

    private static BuiltInGrant ToGrant(string area, string action) =>
        new(
            Guid.FromName(ModuleGrantPrefix, GrantName.Of(area, action)),
            new Grant(GrantName.Of(area, action)),
            area,
            action);

    private sealed record ModuleGrant(ModuleAudience Audience, BuiltInGrant Grant)
    {
        public bool IsFor(BuiltInRole role) =>
            role.Id == BuiltInAccess.Administrators.Id && Audience != ModuleAudience.Developer
            || role.Id == BuiltInAccess.Users.Id && Audience == ModuleAudience.Everyone && Grant.Action == GrantAction.Read
            || role.Id == BuiltInAccess.Developers.Id && Audience == ModuleAudience.Developer
                                                        && Grant.Action is GrantAction.Read or GrantAction.Manage;
    }
}
