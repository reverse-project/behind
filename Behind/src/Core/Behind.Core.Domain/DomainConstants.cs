namespace Behind.Core.Domain;

public static class DomainConstants
{
    public const string System = nameof(System);
    public const string ViewDeletedGrant = "ViewDeleted";
    public const string AdministratorsRole = "Administrators";
    public const string UsersRole = "Users";
    public const string DevelopersRole = "Developers";
    public static Guid SystemUserId => Guid.Parse("10000000-0000-0000-0000-000000000000");
    public static Guid SystemRoleId => Guid.Parse("10000000-0000-0000-0000-000000000000");
    public static Guid GuestUserId => Guid.Parse("20000000-0000-0000-0000-000000000000");
    public static Guid GuestRoleId => Guid.Parse("20000000-0000-0000-0000-000000000000");
    public static readonly TimeSpan AccessTokenDuration = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan RefreshTokenDuration = TimeSpan.FromDays(30);
    public static readonly TimeSpan ImpersonationDuration = TimeSpan.FromHours(1);
}