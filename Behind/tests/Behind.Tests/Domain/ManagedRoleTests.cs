namespace Behind.Tests.Domain;

public class ManagedRoleTests
{
    [Theory]
    [InlineData("administrator")]
    [InlineData("ADMINISTRATOR")]
    public void Parse_Administrator_IsCaseInsensitive(string key)
    {
        Assert.Equal(Some(ManagedRole.Administrator), ManagedRole.Parse(key));
    }

    [Fact]
    public void Parse_Developer_ResolvesTheDevelopersRole()
    {
        var role = ManagedRole.Parse("developer").Match(() => throw new InvalidOperationException(), found => found);

        Assert.Equal(BuiltInAccess.Developers, role.Role);
    }

    [Theory]
    [InlineData("users")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_UnknownKey_ReturnsNone(string? key)
    {
        Assert.Equal(None, ManagedRole.Parse(key));
    }

    [Fact]
    public void RequiredGrant_IsUserManageForAdministratorAndRawDatabaseManageForDeveloper()
    {
        Assert.Equal("User.Manage", ManagedRole.Administrator.RequiredGrant);
        Assert.Equal("RawDatabase.Manage", ManagedRole.Developer.RequiredGrant);
    }

    [Fact]
    public void RequiredGrant_IsHeldByTheRoleThatItGuards()
    {
        Assert.Contains(
            ManagedRole.Administrator.RequiredGrant,
            BuiltInAccess.Administrators.Grants.Select(grant => grant.Grant.Name));
        Assert.Contains(
            ManagedRole.Developer.RequiredGrant,
            BuiltInAccess.Developers.Grants.Select(grant => grant.Grant.Name));
    }
}
