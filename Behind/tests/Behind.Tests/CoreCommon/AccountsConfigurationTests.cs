using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreCommon;

public class AccountsConfigurationTests
{
    private static AccountsConfiguration Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection(AccountsConfiguration.SectionName)
            .Get<AccountsConfiguration>() ?? new AccountsConfiguration();

    [Fact]
    public void SectionName_IsBackendAccounts()
    {
        Assert.Equal("Backend:Accounts", AccountsConfiguration.SectionName);
    }

    [Fact]
    public void Defaults_EnableSignInAndSignUp()
    {
        var configuration = new AccountsConfiguration();

        Assert.True(configuration.SignIn);
        Assert.True(configuration.SignUp);
        Assert.True(configuration.Password);
        Assert.True(configuration.MagicLink);
        Assert.True(configuration.TwoFactor);
        Assert.True(configuration.Passkey);
        Assert.True(configuration.EmailTwoFactor);
    }

    [Fact]
    public void Binding_ReadsEverySwitchSeparately()
    {
        var configuration = Bind(new() { ["Backend:Accounts:SignUp"] = "false" });

        Assert.True(configuration.SignIn);
        Assert.False(configuration.SignUp);
        Assert.False(Bind(new() { ["Backend:Accounts:SignIn"] = "false" }).SignIn);
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("MagicLink")]
    [InlineData("TwoFactor")]
    [InlineData("Passkey")]
    [InlineData("EmailTwoFactor")]
    public void Binding_ReadsSignInModes(string mode)
    {
        var configuration = Bind(new() { [$"Backend:Accounts:{mode}"] = "false" });

        Assert.Equal(mode != "Password", configuration.Password);
        Assert.Equal(mode != "MagicLink", configuration.MagicLink);
        Assert.Equal(mode != "TwoFactor", configuration.TwoFactor);
        Assert.Equal(mode != "Passkey", configuration.Passkey);
        Assert.Equal(mode != "EmailTwoFactor", configuration.EmailTwoFactor);
    }

    [Fact]
    public void Binding_MissingSection_KeepsTheDefaults()
    {
        Assert.Equal(new AccountsConfiguration(), Bind(new()));
    }
}
