using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public sealed record ManagedRole(string Key, BuiltInRole Role, string RequiredGrant)
{
    public static readonly ManagedRole Administrator = new(
        "administrator",
        BuiltInAccess.Administrators,
        GrantName.Of(GrantArea.User, GrantAction.Manage));

    public static readonly ManagedRole Developer = new(
        "developer",
        BuiltInAccess.Developers,
        BuiltInAccess.RawDatabaseManage.Grant.Name);

    public static readonly IReadOnlyList<ManagedRole> All = [Administrator, Developer];

    public static Option<ManagedRole> Parse(string? key) =>
        All.Find(role => string.Equals(role.Key, key, StringComparison.OrdinalIgnoreCase));
}
