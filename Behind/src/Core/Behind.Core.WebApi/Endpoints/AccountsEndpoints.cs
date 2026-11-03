using Behind.Core.Common.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed record AccountsSettingsResponse(bool SignIn, bool SignUp);

public static class AccountsEndpoints
{
    public static IResult GetSettings(IOptionsSnapshot<AccountsConfiguration> configuration) =>
        Results.Json(new AccountsSettingsResponse(configuration.Value.SignIn, configuration.Value.SignUp));
}
