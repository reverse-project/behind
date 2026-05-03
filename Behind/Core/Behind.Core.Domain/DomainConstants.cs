namespace Behind.Core.Domain;

public static class DomainConstants
{
    public const string System = nameof(System);
    public static Guid SystemUserId => Guid.Parse("10000000-0000-0000-0000-000000000000");
    public static Guid SystemRoleId => Guid.Parse("10000000-0000-0000-0000-000000000000");
}