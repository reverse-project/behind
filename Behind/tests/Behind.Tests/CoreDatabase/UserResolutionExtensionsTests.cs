using Behind.Core.Database;

namespace Behind.Tests.CoreDatabase;

public sealed class UserResolutionExtensionsTests
{
    [Fact]
    public void ToUser_SystemUserId_ReturnsSystemUser() =>
        Assert.IsType<SystemUser>(DomainConstants.SystemUserId.ToUser(null, []));

    [Fact]
    public void ToUser_WithDisplayNameAndEmails_ReturnsHumanUser()
    {
        var user = Assert.IsType<HumanUser>(Guid.NewGuid().ToUser("Jane", ["jane@test.com"]));

        Assert.Equal("Jane", user.DisplayName);
        Assert.Equal("jane@test.com", Assert.Single(user.Emails).Value);
    }

    [Fact]
    public void ToUser_WithoutDisplayName_ReturnsUnknownUserWithTheId()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new UnknownUser(id), id.ToUser(null, []));
    }

    [Fact]
    public void ToUser_WithoutEmails_ReturnsUnknownUserWithTheId()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new UnknownUser(id), id.ToUser("Jane", []));
    }
}
