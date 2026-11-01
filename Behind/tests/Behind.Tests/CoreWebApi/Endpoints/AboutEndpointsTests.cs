using Behind.Core.WebApi;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class AboutEndpointsTests
{
    [Fact]
    public void Get_ReturnsTheProductNameAndAssemblyVersion()
    {
        var result = AboutEndpoints.Get();

        var about = Assert.IsType<JsonHttpResult<AboutResponse>>(result).Value!;
        Assert.Equal("Behind", about.Name);
        Assert.Equal(typeof(ICoreWebApi).Assembly.ProductVersion, about.Version);
    }

    [Fact]
    public void ProductVersion_OmitsBuildMetadata()
    {
        var version = typeof(ICoreWebApi).Assembly.ProductVersion;

        Assert.DoesNotContain('+', version);
        Assert.True(Version.TryParse(version.Split('-')[0], out _));
    }
}
