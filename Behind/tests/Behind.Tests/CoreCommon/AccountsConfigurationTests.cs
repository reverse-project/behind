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
    }

    [Fact]
    public void Binding_ReadsEverySwitchSeparately()
    {
        var configuration = Bind(new() { ["Backend:Accounts:SignUp"] = "false" });

        Assert.True(configuration.SignIn);
        Assert.False(configuration.SignUp);
        Assert.False(Bind(new() { ["Backend:Accounts:SignIn"] = "false" }).SignIn);
    }

    [Fact]
    public void Binding_MissingSection_KeepsTheDefaults()
    {
        Assert.Equal(new AccountsConfiguration(), Bind(new()));
    }
}
