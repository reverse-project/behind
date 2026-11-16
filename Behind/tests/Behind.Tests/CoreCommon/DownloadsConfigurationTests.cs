using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreCommon;

public class DownloadsConfigurationTests
{
    private static DownloadsConfiguration Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection(DownloadsConfiguration.SectionName)
            .Get<DownloadsConfiguration>() ?? new DownloadsConfiguration();

    [Fact]
    public void SectionName_IsBackendDownloads()
    {
        Assert.Equal("Backend:Downloads", DownloadsConfiguration.SectionName);
    }

    [Fact]
    public void Defaults_HaveAHundredMegabyteLimit()
    {
        Assert.Equal(100L * 1024 * 1024, new DownloadsConfiguration().FileLimit);
    }

    [Fact]
    public void Binding_ReadsTheLimitFromTheBackendSection()
    {
        Assert.Equal(2048, Bind(new() { ["Backend:Downloads:MaxFileBytes"] = "2048" }).FileLimit);
    }

    [Fact]
    public void Binding_MissingValue_KeepsTheDefault()
    {
        Assert.Equal(DownloadsConfiguration.DefaultMaxFileBytes, Bind(new()).FileLimit);
    }

    [Fact]
    public void Binding_TheBackendConfigurationItselfIgnoresTheDownloadsSection()
    {
        var backend = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Backend:DataDirectory"] = "/srv/data",
                ["Backend:Downloads:MaxFileBytes"] = "10"
            })
            .Build()
            .GetSection(BackendConfiguration.SectionName)
            .Get<BackendConfiguration>()!;

        Assert.Equal(Path.Combine("/srv/data", "vfs"), backend.VfsRoot);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void FileLimit_NonPositive_FallsBackToTheDefault(long value)
    {
        Assert.Equal(
            DownloadsConfiguration.DefaultMaxFileBytes,
            new DownloadsConfiguration { MaxFileBytes = value }.FileLimit);
    }
}
