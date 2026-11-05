using Behind.Common.Configuration;
using Behind.Common.Converters;
using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreCommon;

public class TwoFactorRequiredBindingTests
{
    private static SecurityConfiguration Bind(Dictionary<string, string?> values)
    {
        OptionTypeConverterRegistration.Register<SecretKey>();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        return configuration.GetSection("Security").GetOptionAware<SecurityConfiguration>()!;
    }

    [Fact]
    public void Default_IsOptional()
    {
        Assert.False(new SecurityConfiguration().TwoFactorRequired);
    }

    [Fact]
    public void Bind_NotConfigured_IsOptional()
    {
        Assert.False(Bind(new() { ["Security:ReadOnlyConfiguration"] = "false" }).TwoFactorRequired);
    }

    [Fact]
    public void Bind_True_IsRequired()
    {
        Assert.True(Bind(new() { ["Security:TwoFactorRequired"] = "true" }).TwoFactorRequired);
    }
}
