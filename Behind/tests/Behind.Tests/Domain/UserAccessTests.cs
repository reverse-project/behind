namespace Behind.Tests.Domain;

public class UserAccessTests
{
    private static Role RoleOf(string name, string[] grants, string[] restrictions) =>
        new UserRole(name, Some<IEnumerable<Grant>>(grants.Select(grant => new Grant(grant)).ToList()))
        {
            Restrictions = Some<IEnumerable<Restriction>>(restrictions.Select(name => new Restriction(name)).ToList())
        };

    [Fact]
    public void EffectiveGrants_RoleRestrictionRemovesItsOwnGrant()
    {
        var role = RoleOf("r", ["A.Read", "A.Write"], ["A.Write"]);

        Assert.Equal(["A.Read"], role.EffectiveGrants);
        Assert.True(role.Holds("A.Read"));
        Assert.False(role.Holds("A.Write"));
    }

    [Fact]
    public void GrantNamesAndRestrictionNames_RoleWithoutAccess_AreEmpty()
    {
        var role = new UserRole("empty");

        Assert.Empty(role.GrantNames);
        Assert.Empty(role.RestrictionNames);
        Assert.Empty(role.EffectiveGrants);
    }

    [Fact]
    public void With_ReplacesGrantsAndRestrictions()
    {
        var role = RoleOf("r", ["A.Read"], []);

        var changed = role.With(new AccessSelection(["B.Read"], ["C.Read"]));

        Assert.Equal(["B.Read"], changed.GrantNames);
        Assert.Equal(["C.Read"], changed.RestrictionNames);
        Assert.Equal("r", changed.Name);
    }

    [Fact]
    public void GrantsAddedBy_ListsOnlyGrantsTheProposedRoleGainsEffectively()
    {
        var role = RoleOf("r", ["A.Read", "A.Write"], ["A.Write"]);
        var proposed = role.With(new AccessSelection(["A.Read", "A.Write", "B.Read"], []));

        Assert.Equal(["A.Write", "B.Read"], role.GrantsAddedBy(proposed).Names.Order().ToList());
    }

    [Fact]
    public void CalculatedGrants_UnionsRolesAndDirectGrantsThenAppliesEveryRestriction()
    {
        var access = new UserAccess(
            [RoleOf("a", ["A.Read", "A.Write"], []), RoleOf("b", ["B.Read"], ["A.Write"])],
            ["C.Read", "A.Read"],
            ["B.Read"]);

        Assert.Equal(["A.Read", "C.Read"], access.CalculatedGrants);
    }

    [Fact]
    public void CalculatedGrants_RestrictionWinsOverADirectGrant()
    {
        var access = new UserAccess([], ["A.Read"], ["A.Read"]);

        Assert.Empty(access.CalculatedGrants);
        Assert.False(access.Holds("A.Read"));
    }

    [Fact]
    public void IsDeveloper_RequiresTheRawDatabaseGrant()
    {
        Assert.True(new UserAccess([], [BuiltInAccess.RawDatabaseManage.Grant.Name], []).IsDeveloper);
        Assert.False(UserAccess.Empty.IsDeveloper);
    }

    [Fact]
    public void GrantsAddedBy_ListsWhatTheProposalAddsToTheCalculatedGrants()
    {
        var current = new UserAccess([RoleOf("a", ["A.Read"], [])], [], []);
        var proposed = current.With([RoleOf("a", ["A.Read"], []), RoleOf("b", ["B.Read"], [])], new AccessSelection(["C.Read"], []));

        Assert.Equal(["B.Read", "C.Read"], current.GrantsAddedBy(proposed).Names.Order().ToList());
    }

    [Fact]
    public void GrantsAddedBy_LiftingARestrictionCountsAsAGrant()
    {
        var current = new UserAccess([RoleOf("a", ["A.Read"], [])], [], ["A.Read"]);
        var proposed = current.With(current.Roles, AccessSelection.Empty);

        Assert.Equal(["A.Read"], current.GrantsAddedBy(proposed));
    }

    [Fact]
    public void Normalized_RemovesDuplicatesAndOrdersNames()
    {
        var selection = new AccessSelection(["B.Read", "A.Read", "B.Read"], ["Z.Read", "Z.Read"]).Normalized;

        Assert.Equal(["A.Read", "B.Read"], selection.Grants);
        Assert.Equal(["Z.Read"], selection.Restrictions);
    }

    [Fact]
    public void IsValid_RequiresKnownGrantsAndNoOverlap()
    {
        Assert.True(new AccessSelection(["User.Read"], ["User.Manage"]).IsValid);
        Assert.True(AccessSelection.Empty.IsValid);
        Assert.False(new AccessSelection(["Nope.Read"], []).IsValid);
        Assert.False(new AccessSelection([], ["Nope.Read"]).IsValid);
        Assert.False(new AccessSelection(["User.Read"], ["User.Read"]).IsValid);
    }

    [Fact]
    public void BuiltInRestrictions_MirrorEveryBuiltInGrantWithDistinctIds()
    {
        Assert.Equal(
            BuiltInAccess.Grants.Select(grant => grant.Grant.Name),
            BuiltInAccess.Restrictions.Select(restriction => restriction.Restriction.Name));
        Assert.Equal(
            BuiltInAccess.Restrictions.Count,
            BuiltInAccess.Restrictions.Select(restriction => restriction.Id).Distinct().Count());
        Assert.Empty(BuiltInAccess.Restrictions.Select(restriction => restriction.Id).Intersect(BuiltInAccess.Grants.Select(grant => grant.Id)));
    }

    [Fact]
    public void IsBuiltIn_RecognisesOnlyTheSeededRoles()
    {
        Assert.True(BuiltInAccess.IsBuiltIn(BuiltInAccess.Users.Id));
        Assert.False(BuiltInAccess.IsBuiltIn(Guid.NewGuid()));
    }

    [Fact]
    public void IsDeveloperRole_OnlyForFlaggedUserRoles()
    {
        Assert.True((new UserRole("Ops") { IsDeveloper = true }).IsDeveloperRole);
        Assert.False(new UserRole("Ops").IsDeveloperRole);
        Assert.False(new SystemRole(None).IsDeveloperRole);
    }

    [Fact]
    public void GrantsDeveloperAccess_DetectsDeveloperGrantsOfRolesAndSelections()
    {
        Assert.True(RoleOf("r", ["Configuration.Read"], []).GrantsDeveloperAccess);
        Assert.True(RoleOf("r", ["RawDatabase.Manage"], []).GrantsDeveloperAccess);
        Assert.False(RoleOf("r", ["User.Read"], ["Configuration.Read"]).GrantsDeveloperAccess);
        Assert.True(new AccessSelection(["Configuration.Manage"], []).GrantsDeveloperAccess);
        Assert.False(new AccessSelection(["User.Read"], ["Configuration.Manage"]).GrantsDeveloperAccess);
    }

    [Fact]
    public void DeveloperRolesAddedBy_ListsOnlyNewlyAssignedDeveloperRoles()
    {
        var ops = new UserRole("Ops") { IsDeveloper = true };
        var support = RoleOf("Support", [], []);
        var current = new UserAccess([support], [], []);

        Assert.Equal([ops], current.DeveloperRolesAddedBy(new UserAccess([support, ops], [], [])));
        Assert.Empty(new UserAccess([ops], [], []).DeveloperRolesAddedBy(new UserAccess([ops, support], [], [])));
        Assert.Empty(current.DeveloperRolesAddedBy(current));
    }
}
