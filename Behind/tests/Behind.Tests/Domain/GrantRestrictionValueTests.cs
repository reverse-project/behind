namespace Behind.Tests.Domain;

public class GrantRestrictionValueTests
{
    [Fact]
    public void ToString_ReturnsTheName()
    {
        Assert.Equal("User.Read", new Grant("User.Read").ToString());
        Assert.Equal("User.Read", new Restriction("User.Read").ToString());
        Assert.Equal("Granted: User.Read", $"Granted: {new Grant("User.Read")}");
    }

    [Fact]
    public void ImplicitConversions_RoundTripThroughString()
    {
        Grant grant = "User.Read";
        Restriction restriction = "User.Read";
        string grantName = grant;
        string restrictionName = restriction;

        Assert.Equal(new Grant("User.Read"), grant);
        Assert.Equal(new Restriction("User.Read"), restriction);
        Assert.Equal("User.Read", grantName);
        Assert.Equal("User.Read", restrictionName);
    }

    [Fact]
    public void ValueEquality_AllowsGrantsToBeUsedAsListValues()
    {
        var access = new UserAccess([], ["A.Read", "B.Read"], ["B.Read"]);

        Assert.Contains(new Grant("A.Read"), access.Grants);
        Assert.Contains(new Restriction("B.Read"), access.Restrictions);
        Assert.Equal(new[] { "A.Read", "B.Read" }, access.Grants.Names);
        Assert.Equal(new[] { "B.Read" }, access.Restrictions.Names);
        Assert.Equal(["A.Read"], access.CalculatedGrants);
    }
}
