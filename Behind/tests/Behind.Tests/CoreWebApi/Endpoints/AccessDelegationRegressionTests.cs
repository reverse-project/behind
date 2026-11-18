using Behind.Core.WebApi.Endpoints;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class AccessDelegationRegressionTests
{
    private static Role RoleWith(string[] grants, string[] restrictions) =>
        new UserRole("custom", Some<IEnumerable<Grant>>(grants.Select(value => new Grant(value))))
        {
            Restrictions = Some<IEnumerable<Restriction>>(restrictions.Select(value => new Restriction(value)))
        };

    private static UserManagementActor Actor(UserAccess access) =>
        new(Guid.NewGuid(), false, false, access, AccessCatalog.Core);

    [Theory]
    [InlineData("RawDatabase.Manage", false)]
    [InlineData("Configuration.Manage", false)]
    [InlineData("User.Read", true)]
    public void AddingRestrictionRequiresAuthorityOverTheRestrictedGrant(string restriction, bool expected)
    {
        var role = RoleWith([], []);
        var actor = Actor(new UserAccess([], [(Grant)"User.Read"], []));

        Assert.Equal(expected, actor.CanChangeRole(role, new AccessSelection([], [restriction])));
    }

    [Fact]
    public void AddingAuthorizedRestrictionToOwnRoleDoesNotRaisePrivileges()
    {
        var role = RoleWith(["User.Read"], []);
        var actor = Actor(new UserAccess([role], [], []));

        Assert.True(actor.CanChangeRole(role, new AccessSelection([], ["User.Read"])));
    }

    [Fact]
    public void RemovingRestrictionFromOwnRoleCannotRaisePrivileges()
    {
        var role = RoleWith([], ["User.Read"]);
        var actor = Actor(new UserAccess([role], [], [])) with { IsDeveloper = true };

        Assert.False(actor.CanChangeRole(role, new AccessSelection([], [])));
    }

    [Fact]
    public void DeveloperMayAddRestrictionsOnOtherRoles()
    {
        var actor = Actor(new UserAccess([], [], [])) with { IsDeveloper = true };

        Assert.True(actor.CanChangeRole(RoleWith([], []), new AccessSelection([], ["RawDatabase.Manage"])));
    }

    [Fact]
    public void RemovingRestrictionRequiresAuthorityOverRestoredGrant()
    {
        var role = RoleWith([], ["RawDatabase.Manage"]);
        var actor = Actor(new UserAccess([role], [], []));
        Assert.False(actor.CanChangeRole(role, new AccessSelection([], [])));
        Assert.False(actor.CanDeleteRole(Guid.NewGuid(), role));
    }

    [Fact]
    public void SuppressedDirectDeveloperGrantCannotBeStored()
    {
        var role = RoleWith([], ["RawDatabase.Manage"]);
        var current = new UserAccess([role], [], []);
        var proposed = current with { Grants = [(Grant)"RawDatabase.Manage"] };
        Assert.Empty(proposed.CalculatedGrants);
        Assert.False(Actor(current).CanChangeAccess(current, proposed));
    }
    [Fact]
    public void RestrictedDeveloperMembershipDoesNotBypassResetDelegation()
    {
        var role = RoleWith([], ["RawDatabase.Manage"]);
        var actor = Actor(new UserAccess([BuiltInAccess.Developers.Role], [], [(Restriction)"RawDatabase.Manage"]));
        Assert.False(actor.CanResetRole(role, new AccessSelection([], [])));
    }
}
