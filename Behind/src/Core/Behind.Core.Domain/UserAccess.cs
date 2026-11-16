namespace Behind.Core.Domain;

public sealed record UserAccess(
    IReadOnlyList<Role> Roles,
    IReadOnlyList<Grant> Grants,
    IReadOnlyList<Restriction> Restrictions)
{
    public static readonly UserAccess Empty = new([], [], []);
}
