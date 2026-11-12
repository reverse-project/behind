using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public sealed record BuiltInGrant(Guid Id, Grant Grant, string Area, string Action);

public sealed record BuiltInRestriction(Guid Id, Restriction Restriction);

public sealed record BuiltInRole(Guid Id, UserRole Role)
{
    public IReadOnlyList<BuiltInGrant> Grants { get; init; } = [];
}

public static class BuiltInAccess
{
    private const int RawDatabaseManageNumber = 100;

    public static readonly IReadOnlyList<BuiltInGrant> AreaGrants =
    [
        ..GrantArea.All.SelectMany((area, areaIndex) =>
            GrantAction.All
                .Select((action, actionIndex) => (action, actionIndex))
                .Where(entry => GrantArea.ActionsOf(area).Contains(entry.action))
                .Select(entry => NewGrant(areaIndex * 10 + entry.actionIndex + 1, area, entry.action)))
    ];

    public static readonly BuiltInGrant RawDatabaseManage =
        NewGrant(RawDatabaseManageNumber, GrantArea.RawDatabase, GrantAction.Manage);

    public static readonly IReadOnlyList<BuiltInGrant> Grants = [..AreaGrants, RawDatabaseManage];

    public static readonly IReadOnlyList<BuiltInRestriction> Restrictions =
    [
        ..Grants.Select(RestrictionOf)
    ];

    public static readonly BuiltInRole Administrators = NewRole(
        3,
        DomainConstants.AdministratorsRole,
        AreaGrants.Where(grant => !grant.IsDeveloperOnly).ToList(),
        false);

    public static readonly BuiltInRole Users = NewRole(
        4,
        DomainConstants.UsersRole,
        AreaGrants.Where(grant => (grant.Action == GrantAction.Read || grant.Area == GrantArea.Chat && grant.Action == GrantAction.Write)
            && !grant.IsDeveloperOnly && !grant.IsAdministrationOnly).ToList(),
        false);

    public static readonly BuiltInRole Developers = NewRole(
        5,
        DomainConstants.DevelopersRole,
        [..AreaGrants.Where(grant => grant.IsDeveloperOnly && grant.Action is GrantAction.Read or GrantAction.Manage), RawDatabaseManage],
        true);

    public static readonly IReadOnlyList<BuiltInRole> Roles = [Administrators, Users, Developers];

    public static readonly IReadOnlyList<string> DeveloperGrantNames =
    [
        ..Grants.Where(grant => grant.IsDeveloperOnly || grant == RawDatabaseManage).Select(grant => grant.Grant.Name)
    ];

    public static BuiltInRestriction RestrictionOf(BuiltInGrant grant) =>
        new(Guid.Parse($"70000000-{grant.Id.ToString()[9..]}"), new Restriction(grant.Grant.Name));

    public static bool IsBuiltIn(Guid roleId) => Roles.Any(role => role.Id == roleId);

    public static Option<BuiltInRole> FindRole(Guid roleId) =>
        Roles.Find(role => role.Id == roleId);

    public static bool IsDeveloperGrant(string name) => DeveloperGrantNames.Contains(name);

    public static IReadOnlyList<string> GrantNames { get; } = [..Grants.Select(grant => grant.Grant.Name)];

    public static bool IsKnownGrant(string name) => GrantNames.Contains(name);

    extension(BuiltInGrant grant)
    {
        private bool IsDeveloperOnly => grant.Area == GrantArea.Configuration;

        private bool IsAdministrationOnly => grant.Area is GrantArea.AuditLog or GrantArea.Impersonation or GrantArea.Site or GrantArea.News or GrantArea.Article or GrantArea.Menu or GrantArea.Gallery or GrantArea.Download or GrantArea.RouteAlias or GrantArea.Access or GrantArea.Message;
    }

    private static BuiltInGrant NewGrant(int number, string area, string action) =>
        new(
            Guid.Parse($"60000000-0000-0000-0000-{number:D12}"),
            new Grant(GrantName.Of(area, action)),
            area,
            action);

    private static BuiltInRole NewRole(int number, string name, IReadOnlyList<BuiltInGrant> grants, bool isDeveloper) =>
        new(
            Guid.Parse($"{number}0000000-0000-0000-0000-000000000000"),
            new UserRole(name, Some<IEnumerable<Grant>>(grants.Select(grant => grant.Grant).ToList()))
            {
                IsDeveloper = isDeveloper
            })
        {
            Grants = grants
        };
}
