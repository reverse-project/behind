namespace Behind.Tests.Domain;

public class AccessSettingsTests
{
    private static UserAccess AccessOf(string roleName, params string[] grants) =>
        new(
            [new UserRole(roleName)],
            grants.Select(grant => new Grant(grant)).ToList(),
            []);

    private static AccessSettings Restricting(string role = "Users") =>
        new(true, role, "Members only", false);

    [Fact]
    public void Open_RestrictsNobodyAndNamesTheUsersRole()
    {
        Assert.False(AccessSettings.Open.RestrictionEnabled);
        Assert.False(AccessSettings.Open.ModeratedSignUp);
        Assert.Equal(DomainConstants.UsersRole, AccessSettings.Open.RestrictedToRole);
        Assert.False(AccessSettings.Open.IsRestrictedFor(UserAccess.Empty));
    }

    [Fact]
    public void NewSettings_HaveAnEmptyIdAndKeepTheirFields()
    {
        var settings = new AccessSettings(true, "Users", "Hello", true);

        Assert.Equal(new VersionedKey<Guid>(Guid.Empty), settings.Id);
        Assert.True(settings.RestrictionEnabled);
        Assert.Equal("Hello", settings.RestrictionMessage);
        Assert.True(settings.ModeratedSignUp);
    }

    [Fact]
    public void IsRestrictedFor_AnonymousVisitor_IsRestricted()
    {
        Assert.True(Restricting().IsRestrictedFor(UserAccess.Empty));
    }

    [Fact]
    public void IsRestrictedFor_UserHoldingTheRequiredRole_IsNotRestricted()
    {
        Assert.False(Restricting().IsRestrictedFor(AccessOf("Users")));
    }

    [Fact]
    public void IsRestrictedFor_RoleNameComparisonIgnoresCase()
    {
        Assert.False(Restricting("users").IsRestrictedFor(AccessOf("USERS")));
    }

    [Fact]
    public void IsRestrictedFor_UserWithAnotherRole_IsRestricted()
    {
        Assert.True(Restricting("Members").IsRestrictedFor(AccessOf("Users")));
    }

    [Fact]
    public void IsRestrictedFor_AccessSettingsManager_IsNeverRestricted()
    {
        Assert.False(
            Restricting("Members").IsRestrictedFor(AccessOf("Administrators", AccessSettingsExtensions.ManageGrant)));
    }

    [Fact]
    public void VfsTypes_AccessSettingsHaveTheirOwnKindAndUrl()
    {
        Assert.Equal(VfsTypes.AccessImages, VfsTypes.SameKindAs(VfsTypes.AccessSettings));
        Assert.Equal(VfsTypes.AccessImageUrl, VfsTypes.ImageUrlOf(VfsTypes.AccessSettings));
        Assert.DoesNotContain(VfsTypes.AccessSettings, VfsTypes.AllSiteImages);
        Assert.Equal("/access/images", VfsTypes.AccessImageUrl);
    }

    [Fact]
    public void ManageGrant_IsTheAccessManageGrant()
    {
        Assert.Equal("Access.Manage", AccessSettingsExtensions.ManageGrant);
    }

    [Fact]
    public void HasRole_FindsTheRoleByName()
    {
        Assert.True(AccessOf("Users").HasRole("users"));
        Assert.False(AccessOf("Users").HasRole("Developers"));
        Assert.False(UserAccess.Empty.HasRole("Users"));
    }
}
