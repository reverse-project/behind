namespace Behind.Tests.Domain;

public class BuiltInAccessTests
{
    private const string ConfigurationRead = "Configuration.Read";
    private const string RawDatabaseManage = "RawDatabase.Manage";

    private static IReadOnlyList<string> GrantNames(BuiltInRole role) =>
        role.Grants.Select(grant => grant.Grant.Name).Order().ToList();

    private static IReadOnlyList<string> AreaGrantNames =>
        GrantArea.All
            .SelectMany(area => GrantAction.All.Select(action => GrantName.Of(area, action)))
            .Order()
            .ToList();

    [Fact]
    public void Grants_CoverReadWriteDeleteAndManageForEveryAreaPlusRawDatabaseManage()
    {
        Assert.Equal(33, BuiltInAccess.Grants.Count);
        Assert.Equal(
            AreaGrantNames.Append(RawDatabaseManage).Order(),
            BuiltInAccess.Grants.Select(grant => grant.Grant.Name).Order());
        Assert.Equal(RawDatabaseManage, BuiltInAccess.RawDatabaseManage.Grant.Name);
    }

    [Fact]
    public void Administrators_HaveEveryAreaGrantExceptConfiguration()
    {
        Assert.Equal(DomainConstants.AdministratorsRole, BuiltInAccess.Administrators.Role.Name);
        Assert.Equal(
            AreaGrantNames.Where(name => !name.StartsWith("Configuration.")),
            GrantNames(BuiltInAccess.Administrators));
    }

    [Fact]
    public void Users_HaveReadOnEveryAreaExceptConfigurationAuditLogImpersonationSiteAndNews()
    {
        Assert.Equal(DomainConstants.UsersRole, BuiltInAccess.Users.Role.Name);
        Assert.Equal(
            GrantArea.All
                .Where(area => area is not (GrantArea.Configuration or GrantArea.AuditLog or GrantArea.Impersonation or GrantArea.Site or GrantArea.News))
                .Select(area => GrantName.Of(area, GrantAction.Read))
                .Order(),
            GrantNames(BuiltInAccess.Users));
    }

    [Fact]
    public void Developers_HaveReadOnEveryAreaAndRawDatabaseManage()
    {
        Assert.Equal(DomainConstants.DevelopersRole, BuiltInAccess.Developers.Role.Name);
        Assert.Equal(
            GrantArea.All.Select(area => GrantName.Of(area, GrantAction.Read)).Append(RawDatabaseManage).Order(),
            GrantNames(BuiltInAccess.Developers));
    }

    [Fact]
    public void ConfigurationRead_BelongsOnlyToDevelopers()
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(ConfigurationRead))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.DevelopersRole], holders);
    }

    [Fact]
    public void ImpersonationManage_BelongsOnlyToAdministrators()
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.Impersonation, GrantAction.Manage)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
    }

    [Theory]
    [InlineData(GrantAction.Write)]
    [InlineData(GrantAction.Manage)]
    public void SiteChanges_BelongOnlyToAdministrators(string action)
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.Site, action)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
    }

    [Fact]
    public void SiteRead_BelongsToAdministratorsAndDevelopers()
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.Site, GrantAction.Read)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole, DomainConstants.DevelopersRole], holders);
    }

    [Theory]
    [InlineData(GrantAction.Write)]
    [InlineData(GrantAction.Delete)]
    [InlineData(GrantAction.Manage)]
    public void NewsChanges_BelongOnlyToAdministrators(string action)
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.News, action)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
    }

    [Fact]
    public void NewsRead_BelongsToAdministratorsAndDevelopers()
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.News, GrantAction.Read)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole, DomainConstants.DevelopersRole], holders);
    }

    [Fact]
    public void RawDatabaseManage_BelongsOnlyToDevelopers()
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(RawDatabaseManage))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.DevelopersRole], holders);
    }

    [Fact]
    public void RoleGrants_AreExposedOnTheDomainRole()
    {
        var grants = BuiltInAccess.Developers.Role.Grants.GetOrElse([]);

        Assert.Equal(9, grants.Count());
    }

    [Fact]
    public void Ids_AreUniqueAndDistinctFromBuiltInUsers()
    {
        Guid[] ids =
        [
            ..BuiltInAccess.Grants.Select(grant => grant.Id),
            ..BuiltInAccess.Roles.Select(role => role.Id),
            DomainConstants.SystemRoleId,
            DomainConstants.GuestRoleId
        ];

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }
}
