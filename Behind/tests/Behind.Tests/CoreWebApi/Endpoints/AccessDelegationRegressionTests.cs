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
