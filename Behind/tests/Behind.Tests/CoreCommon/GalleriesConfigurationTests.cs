using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreCommon;

public class GalleriesConfigurationTests
{
    private static GalleriesConfiguration Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection(GalleriesConfiguration.SectionName)
            .Get<GalleriesConfiguration>() ?? new GalleriesConfiguration();

    [Fact]
    public void SectionName_IsFrontendGalleries()
    {
        Assert.Equal("Frontend:Galleries", GalleriesConfiguration.SectionName);
    }

    [Fact]
    public void Defaults_HaveATenMegabyteLimit()
    {
        Assert.Equal(10L * 1024 * 1024, new GalleriesConfiguration().ImageLimit);
    }

    [Fact]
    public void Binding_ReadsTheLimit()
    {
        Assert.Equal(2048, Bind(new() { ["Frontend:Galleries:MaxImageBytes"] = "2048" }).ImageLimit);
    }

    [Fact]
    public void Binding_MissingValue_KeepsTheDefault()
    {
        Assert.Equal(GalleriesConfiguration.DefaultMaxImageBytes, Bind(new()).ImageLimit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ImageLimit_NonPositive_FallsBackToTheDefault(long value)
    {
        Assert.Equal(
            GalleriesConfiguration.DefaultMaxImageBytes,
            new GalleriesConfiguration { MaxImageBytes = value }.ImageLimit);
    }
}
