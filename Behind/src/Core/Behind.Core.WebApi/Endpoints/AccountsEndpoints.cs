using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed record AccountsSettingsResponse(
    bool SignIn, bool SignUp, bool Password = true, bool MagicLink = true, bool TwoFactor = true, bool Passkey = true,
    bool EmailTwoFactor = true);

public static class AccountsEndpoints
{
    public static IResult GetSettings(IOptionsSnapshot<AccountsConfiguration> configuration) =>
        Results.Json(new AccountsSettingsResponse(
            configuration.Value.SignIn, configuration.Value.SignUp, configuration.Value.Password,
            configuration.Value.MagicLink, configuration.Value.TwoFactor, configuration.Value.Passkey,
            configuration.Value.EmailTwoFactor));
}
