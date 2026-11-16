using Behind.Common.Configuration;
using Behind.Common.Converters;
using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreCommon;

public class ElevationSettingsBindingTests
{
    private static SecurityConfiguration Bind(Dictionary<string, string?> values)
    {
        OptionTypeConverterRegistration.Register<SecretKey>();
        OptionTypeConverterRegistration.Register<ElevationSettings>();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        return configuration.GetSection("Security").GetOptionAware<SecurityConfiguration>()!;
    }

    [Fact]
    public void Bind_NoElevationSection_IsDisabled()
    {
        var security = Bind(new() { ["Security:ReadOnlyConfiguration"] = "false" });

        Assert.Equal(None, security.Elevation);
        Assert.False(security.IsElevationEnabled);
    }

    [Fact]
    public void Bind_EnabledTrue_IsEnabled()
    {
        var security = Bind(new() { ["Security:Elevation:Enabled"] = "true" });

        Assert.True(security.IsElevationEnabled);
    }

    [Fact]
    public void Bind_EnabledFalse_IsDisabled()
    {
        var security = Bind(new() { ["Security:Elevation:Enabled"] = "false" });

        Assert.False(security.IsElevationEnabled);
    }

    [Fact]
    public void Bind_DeveloperSecretKey_IsBoundAsTotp()
    {
        var security = Bind(new()
        {
            ["Security:Elevation:Enabled"] = "true",
            ["Security:Elevation:DeveloperSecretKey"] = "totp://JBSWY3DPEHPK3PXP"
        });

        Totp.TryCreate("JBSWY3DPEHPK3PXP", out var expected);
        Assert.Equal(Some<SecretKey>(expected), security.DeveloperElevationSecretKey);
    }

    [Fact]
    public void Bind_WithoutDeveloperSecretKey_HasNone()
    {
        var security = Bind(new() { ["Security:Elevation:Enabled"] = "true" });

        Assert.Equal(None, security.DeveloperElevationSecretKey);
    }
}
