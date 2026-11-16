using Behind.Core.WebApi;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class AboutEndpointsTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => KeyValuePair.Create<string, string?>(value.Key, value.Value)))
            .Build();

    [Fact]
    public void Get_ReturnsTheProductNameAndAssemblyVersion()
    {
        var result = AboutEndpoints.Get(Configuration());

        var about = Assert.IsType<JsonHttpResult<AboutResponse>>(result).Value!;
        Assert.Equal("Behind", about.Name);
        Assert.Equal(typeof(ICoreWebApi).Assembly.ProductVersion, about.Version);
    }

    [Fact]
    public void Get_ReturnsTheTitleAndCopyrightOfTheFrontendSite()
    {
        var result = AboutEndpoints.Get(
            Configuration(
                ("Frontend:Site:Title", "Reverse Project"),
                ("Frontend:Site:Copyright", "© 2026 Reverse Project")
            )
        );

        var about = Assert.IsType<JsonHttpResult<AboutResponse>>(result).Value!;
        Assert.Equal("Reverse Project", about.Title);
        Assert.Equal("© 2026 Reverse Project", about.Copyright);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Get_FallsBackToTheProductNameAndAnEmptyCopyright(string? title)
    {
        var result = AboutEndpoints.Get(Configuration(("Frontend:Site:Title", title ?? string.Empty)));

        var about = Assert.IsType<JsonHttpResult<AboutResponse>>(result).Value!;
        Assert.Equal("Behind", about.Title);
        Assert.Equal(string.Empty, about.Copyright);
    }

    [Fact]
    public void Get_ReturnsTheDefaultLocaleOfTheFrontendSite()
    {
        var result = AboutEndpoints.Get(Configuration(("Frontend:Site:DefaultLocale", "hu-HU")));

        Assert.Equal("hu-HU", Assert.IsType<JsonHttpResult<AboutResponse>>(result).Value!.DefaultLocale);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Get_FallsBackToTheInternationalEnglishLocale(string? locale)
    {
        var result = AboutEndpoints.Get(Configuration(("Frontend:Site:DefaultLocale", locale ?? string.Empty)));

        Assert.Equal("en-US", Assert.IsType<JsonHttpResult<AboutResponse>>(result).Value!.DefaultLocale);
    }

    [Fact]
    public void ProductVersion_OmitsBuildMetadata()
    {
        var version = typeof(ICoreWebApi).Assembly.ProductVersion;

        Assert.DoesNotContain('+', version);
        Assert.True(Version.TryParse(version.Split('-')[0], out _));
    }
}
