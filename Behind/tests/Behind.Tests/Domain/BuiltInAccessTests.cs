namespace Behind.Tests.Domain;

public class BuiltInAccessTests
{
    private const string ConfigurationRead = "Configuration.Read";
    private const string ConfigurationManage = "Configuration.Manage";
    private const string RawDatabaseManage = "RawDatabase.Manage";

    private static IReadOnlyList<string> GrantNames(BuiltInRole role) =>
        role.Grants.Select(grant => grant.Grant.Name).Order().ToList();

    private static IReadOnlyList<string> AreaGrantNames =>
        GrantArea.All
            .SelectMany(area => GrantArea.ActionsOf(area).Select(action => GrantName.Of(area, action)))
            .Order()
            .ToList();

    [Fact]
    public void Grants_CoverReadWriteDeleteAndManageForEveryAreaPlusRawDatabaseManage()
    {
        Assert.Equal(50, BuiltInAccess.Grants.Count);
        Assert.Equal(
            AreaGrantNames.Append(RawDatabaseManage).Order(),
            BuiltInAccess.Grants.Select(grant => grant.Grant.Name).Order());
        Assert.Equal(RawDatabaseManage, BuiltInAccess.RawDatabaseManage.Grant.Name);
    }

    [Fact]
    public void FindRole_ReturnsTheBuiltInRoleOrNothing()
    {
        Assert.Equal(Some(BuiltInAccess.Users), BuiltInAccess.FindRole(BuiltInAccess.Users.Id));
        Assert.Equal(None, BuiltInAccess.FindRole(Guid.NewGuid()));
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
    public void Users_HaveReadOnEveryAreaExceptConfigurationAuditLogImpersonationSiteNewsArticleMenuGalleryDownloadAndRouteAlias()
    {
        Assert.Equal(DomainConstants.UsersRole, BuiltInAccess.Users.Role.Name);
        Assert.Equal(
            GrantArea.All
                .Where(area => area is not (GrantArea.Configuration or GrantArea.AuditLog or GrantArea.Impersonation or GrantArea.Site or GrantArea.News or GrantArea.Article or GrantArea.Menu or GrantArea.Gallery or GrantArea.Download or GrantArea.RouteAlias))
                .Select(area => GrantName.Of(area, GrantAction.Read))
                .Order(),
            GrantNames(BuiltInAccess.Users));
    }

    [Fact]
    public void Developers_HaveOnlyConfigurationReadConfigurationManageAndRawDatabaseManage()
    {
        Assert.Equal(DomainConstants.DevelopersRole, BuiltInAccess.Developers.Role.Name);
        Assert.Equal(
            new[] { ConfigurationRead, ConfigurationManage, RawDatabaseManage }.Order(),
            GrantNames(BuiltInAccess.Developers));
    }

    [Fact]
    public void ConfigurationManage_BelongsOnlyToDevelopers()
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(ConfigurationManage))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.DevelopersRole], holders);
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
    public void Impersonation_HasOnlyTheManageGrant()
    {
        Assert.Equal([GrantAction.Manage], GrantArea.ActionsOf(GrantArea.Impersonation));
        Assert.Equal(
            ["Impersonation.Manage"],
            BuiltInAccess.Grants.Select(grant => grant.Grant.Name).Where(name => name.StartsWith("Impersonation.")));
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
    public void SiteRead_BelongsOnlyToAdministrators()
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.Site, GrantAction.Read)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
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
    public void NewsRead_BelongsOnlyToAdministrators()
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.News, GrantAction.Read)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
    }

    [Fact]
    public void ArticleRead_BelongsOnlyToAdministrators()
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.Article, GrantAction.Read)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
    }

    [Theory]
    [InlineData(GrantAction.Write)]
    [InlineData(GrantAction.Delete)]
    [InlineData(GrantAction.Manage)]
    public void ArticleChanges_BelongOnlyToAdministrators(string action)
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.Article, action)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
    }

    [Fact]
    public void MenuRead_BelongsOnlyToAdministrators()
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.Menu, GrantAction.Read)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
    }

    [Theory]
    [InlineData(GrantAction.Write)]
    [InlineData(GrantAction.Delete)]
    [InlineData(GrantAction.Manage)]
    public void MenuChanges_BelongOnlyToAdministrators(string action)
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.Menu, action)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
    }

    [Theory]
    [InlineData(GrantAction.Read)]
    [InlineData(GrantAction.Write)]
    [InlineData(GrantAction.Delete)]
    [InlineData(GrantAction.Manage)]
    public void RouteAliasGrants_BelongOnlyToAdministrators(string action)
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.RouteAlias, action)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
    }

    [Fact]
    public void GalleryRead_BelongsOnlyToAdministrators()
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.Gallery, GrantAction.Read)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
    }

    [Theory]
    [InlineData(GrantAction.Write)]
    [InlineData(GrantAction.Delete)]
    [InlineData(GrantAction.Manage)]
    public void GalleryChanges_BelongOnlyToAdministrators(string action)
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.Gallery, action)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
    }

    [Fact]
    public void DownloadRead_BelongsOnlyToAdministrators()
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.Download, GrantAction.Read)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
    }

    [Theory]
    [InlineData(GrantAction.Write)]
    [InlineData(GrantAction.Delete)]
    [InlineData(GrantAction.Manage)]
    public void DownloadChanges_BelongOnlyToAdministrators(string action)
    {
        var holders = BuiltInAccess.Roles
            .Where(role => GrantNames(role).Contains(GrantName.Of(GrantArea.Download, action)))
            .Select(role => role.Role.Name);

        Assert.Equal([DomainConstants.AdministratorsRole], holders);
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

        Assert.Equal(3, grants.Count());
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

    [Fact]
    public void DeveloperGrants_AreConfigurationAndRawDatabase_AndOnlyTheDevelopersRoleHoldsThem()
    {
        Assert.Equal(
            GrantArea.All.Where(area => area == GrantArea.Configuration)
                .SelectMany(area => GrantAction.All.Select(action => GrantName.Of(area, action)))
                .Append(RawDatabaseManage)
                .Order(),
            BuiltInAccess.DeveloperGrantNames.Order());
        Assert.DoesNotContain(BuiltInAccess.Administrators.Grants, grant => BuiltInAccess.IsDeveloperGrant(grant.Grant.Name));
        Assert.DoesNotContain(BuiltInAccess.Users.Grants, grant => BuiltInAccess.IsDeveloperGrant(grant.Grant.Name));
        Assert.Contains(BuiltInAccess.Developers.Grants, grant => BuiltInAccess.IsDeveloperGrant(grant.Grant.Name));
        Assert.False(BuiltInAccess.IsDeveloperGrant("User.Manage"));
    }

    [Fact]
    public void OnlyTheDevelopersRole_IsFlaggedAsDeveloperRole()
    {
        Assert.Equal([BuiltInAccess.Developers.Id], BuiltInAccess.Roles.Where(role => role.Role.IsDeveloperRole).Select(role => role.Id));
    }
}
