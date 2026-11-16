using Behind.Common.Configuration;
using Behind.Common.Converters;
using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreCommon;

public class EmailConfirmationBindingTests
{
    private static SecurityConfiguration Bind(Dictionary<string, string?> values)
    {
        OptionTypeConverterRegistration.Register<SecretKey>();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        return configuration.GetSection("Security").GetOptionAware<SecurityConfiguration>()!;
    }

    [Fact]
    public void Bind_NotConfigured_IsEnabled()
    {
        Assert.True(Bind(new() { ["Security:ReadOnlyConfiguration"] = "false" }).EmailConfirmation);
    }

    [Fact]
    public void Bind_False_IsDisabled()
    {
        Assert.False(Bind(new() { ["Security:EmailConfirmation"] = "false" }).EmailConfirmation);
    }

    [Fact]
    public void Bind_True_IsEnabled()
    {
        Assert.True(Bind(new() { ["Security:EmailConfirmation"] = "true" }).EmailConfirmation);
    }

    [Fact]
    public void Default_IsEnabled()
    {
        Assert.True(new SecurityConfiguration().EmailConfirmation);
    }
}
