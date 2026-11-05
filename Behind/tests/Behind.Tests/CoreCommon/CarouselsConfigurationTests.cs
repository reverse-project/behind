using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreCommon;

public class CarouselsConfigurationTests
{
    private static CarouselsConfiguration Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection(CarouselsConfiguration.SectionName)
            .Get<CarouselsConfiguration>() ?? new CarouselsConfiguration();

    private static CarouselsConfiguration Bound(string name, string value) =>
        Bind(new() { [$"{CarouselsConfiguration.SectionName}:{name}"] = value });

    [Fact]
    public void SectionName_IsFrontendCarousels()
    {
        Assert.Equal("Frontend:Carousels", CarouselsConfiguration.SectionName);
    }

    [Fact]
    public void Defaults_UseTheDocumentedValues()
    {
        var configuration = new CarouselsConfiguration();

        Assert.Equal(20, configuration.SlideLimit);
        Assert.Equal((2, 60), configuration.IntervalRange);
        Assert.Equal(5, configuration.DefaultInterval);
        Assert.Equal(300, configuration.CaptionLimit);
        Assert.Equal(5L * 1024 * 1024, configuration.ImageLimit);
    }

    [Fact]
    public void Binding_MissingSection_KeepsTheDefaults()
    {
        Assert.Equal(new CarouselsConfiguration(), Bind(new()));
    }

    [Fact]
    public void Binding_ReadsEveryValue()
    {
        var configuration = Bind(new()
        {
            ["Frontend:Carousels:MaxSlides"] = "7",
            ["Frontend:Carousels:MinIntervalSeconds"] = "3",
            ["Frontend:Carousels:MaxIntervalSeconds"] = "30",
            ["Frontend:Carousels:IntervalSeconds"] = "8",
            ["Frontend:Carousels:MaxCaptionLength"] = "120",
            ["Frontend:Carousels:MaxImageBytes"] = "1048576"
        });

        Assert.Equal(7, configuration.SlideLimit);
        Assert.Equal((3, 30), configuration.IntervalRange);
        Assert.Equal(8, configuration.DefaultInterval);
        Assert.Equal(120, configuration.CaptionLimit);
        Assert.Equal(1048576, configuration.ImageLimit);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-4")]
    public void NonPositiveLimits_FallBackToTheDefaults(string value)
    {
        Assert.Equal(CarouselsConfiguration.DefaultMaxSlides, Bound("MaxSlides", value).SlideLimit);
        Assert.Equal(CarouselsConfiguration.DefaultMaxCaptionLength, Bound("MaxCaptionLength", value).CaptionLimit);
        Assert.Equal(CarouselsConfiguration.DefaultMaxImageBytes, Bound("MaxImageBytes", value).ImageLimit);
    }

    [Fact]
    public void CaptionLimit_IsCappedAtTheStorageCeiling()
    {
        Assert.Equal(CarouselsConfiguration.CaptionCeiling, Bound("MaxCaptionLength", "100000").CaptionLimit);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(20, 10)]
    public void InconsistentIntervalRange_FallsBackToTheDefaultRange(int min, int max)
    {
        var configuration = new CarouselsConfiguration { MinIntervalSeconds = min, MaxIntervalSeconds = max };

        Assert.Equal((2, 60), configuration.IntervalRange);
    }

    [Theory]
    [InlineData(1, 10, 5, 5)]
    [InlineData(4, 10, 2, 4)]
    [InlineData(4, 10, 99, 10)]
    public void DefaultInterval_IsKeptInsideTheRange(int min, int max, int interval, int expected)
    {
        var configuration = new CarouselsConfiguration
        {
            MinIntervalSeconds = min,
            MaxIntervalSeconds = max,
            IntervalSeconds = interval
        };

        Assert.Equal(expected, configuration.DefaultInterval);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void IsValidInterval_FollowsTheConfiguredRange(int seconds, bool expected)
    {
        Assert.Equal(expected, new CarouselsConfiguration().IsValidInterval(seconds));
    }

    [Fact]
    public void IsValidInterval_UsesTheConfiguredBounds()
    {
        var configuration = new CarouselsConfiguration { MinIntervalSeconds = 10, MaxIntervalSeconds = 120 };

        Assert.False(configuration.IsValidInterval(5));
        Assert.True(configuration.IsValidInterval(120));
        Assert.False(configuration.IsValidInterval(121));
    }
}
