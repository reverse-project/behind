using Behind.Core.Domain;

namespace Behind.Tests.Domain;

public class RouteTargetExtensionsTests
{
    [Theory]
    [InlineData("#/downloads")]
    [InlineData("#/downloads/products/manuals")]
    [InlineData("#/news?page=2")]
    public void IsValidRouteTarget_InternalRoutes_AreAccepted(string target)
    {
        Assert.True(target.IsValidRouteTarget);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#/")]
    [InlineData("downloads")]
    [InlineData("/downloads")]
    [InlineData("https://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("#/down loads")]
    [InlineData("#/down\tloads")]
    [InlineData("#/down\\loads")]
    public void IsValidRouteTarget_AnythingElse_IsRejected(string? target)
    {
        Assert.False(target.IsValidRouteTarget);
    }

    [Fact]
    public void IsValidRouteTarget_TooLongTarget_IsRejected()
    {
        Assert.True($"#/{new string('a', RouteAlias.MaxTargetLength - 2)}".IsValidRouteTarget);
        Assert.False($"#/{new string('a', RouteAlias.MaxTargetLength - 1)}".IsValidRouteTarget);
    }

    [Fact]
    public void NewRouteAlias_HasAnEmptyIdAndKeepsItsFields()
    {
        var alias = new RouteAlias("manuals", "#/downloads");

        Assert.Equal(Guid.Empty, alias.Id.Key);
        Assert.Equal(("manuals", "#/downloads"), (alias.Alias, alias.Target));
    }
}
