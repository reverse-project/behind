using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class AccountsEndpointsTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void GetSettings_ReportsBothSwitches(bool signIn, bool signUp)
    {
        var result = AccountsEndpoints.GetSettings(
            FixedOptionsSnapshot.Of(new AccountsConfiguration { SignIn = signIn, SignUp = signUp }));

        Assert.Equal(
            new AccountsSettingsResponse(signIn, signUp),
            Assert.IsType<JsonHttpResult<AccountsSettingsResponse>>(result).Value);
    }
}
