using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreCommon;

public class SitesConfigurationTests
{
    private static SitesConfiguration Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection(SitesConfiguration.SectionName)
            .Get<SitesConfiguration>() ?? new SitesConfiguration();

    [Fact]
    public void SectionName_LivesUnderTheFrontendSection()
    {
        Assert.Equal("Frontend:Sites", SitesConfiguration.SectionName);
    }

    [Fact]
    public void Defaults_HaveNoIndexAndAFiveMegabyteLimit()
    {
        var configuration = new SitesConfiguration();

        Assert.Null(configuration.IndexSlug);
        Assert.Equal(5L * 1024 * 1024, configuration.ImageLimit);
    }

    [Fact]
    public void Binding_ReadsTheIndexSlugAndTheLimit()
    {
        var configuration = Bind(new()
        {
            ["Frontend:Sites:IndexSlug"] = "home",
            ["Frontend:Sites:MaxImageBytes"] = "1024"
        });

        Assert.Equal("home", configuration.IndexSlug);
        Assert.Equal(1024, configuration.ImageLimit);
    }

    [Fact]
    public void Binding_MissingValues_KeepTheDefaults()
    {
        var configuration = Bind(new() { ["Frontend:Sites:IndexSlug"] = "home" });

        Assert.Equal(SitesConfiguration.DefaultMaxImageBytes, configuration.ImageLimit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ImageLimit_NonPositive_FallsBackToTheDefault(long value)
    {
        Assert.Equal(
            SitesConfiguration.DefaultMaxImageBytes,
            new SitesConfiguration { MaxImageBytes = value }.ImageLimit);
    }
}
