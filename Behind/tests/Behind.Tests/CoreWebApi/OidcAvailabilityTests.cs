using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Oidc;
using Behind.Core.WebApi.TwoFactor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi;

public sealed class OidcAvailabilityTests
{
    [Fact]
    public async Task BackendAccountsOidcReloadControlsListingChallengeRedemptionAndAccountResolution()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Backend:Accounts:Oidc"] = "true"
        }).Build();
        await using var services = new ServiceCollection()
            .AddOptions()
            .Configure<AccountsConfiguration>(configuration.GetSection(AccountsConfiguration.SectionName))
            .BuildServiceProvider();
        var catalog = new OidcProviderCatalog(new()
        {
            Providers = [new()
            {
                Id = "company", DisplayName = "Company", Authority = "https://id.example.test", ClientId = "behind"
            }]
        }, services.GetRequiredService<IOptionsMonitor<AccountsConfiguration>>());
        var tickets = new OidcTicketStore(catalog, TimeProvider.System);
        var identity = new OidcIdentity("company", "https://id.example.test", "subject", null, false, null, []);
        var ticket = tickets.Issue(identity, "flow", "proof");
        var accounts = Options.Create(new AccountsConfiguration());
        var security = Options.Create(new SecurityConfiguration(false, None,
            Some(new JwtSettings("issuer", "audience", new string('k', 64))), None));
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Headers.Cookie = $"{OidcExtensions.CookieName("flow")}=proof";
        Assert.NotNull(catalog.Find("company"));
        Assert.IsType<ChallengeHttpResult>(OidcSignInEndpoints.Challenge("company", catalog, accounts, security,
            context, NullLogger<OidcSignInEndpoints>.Instance));

        configuration["Backend:Accounts:Oidc"] = "false";
        configuration.Reload();
        Assert.False(catalog.IsEnabled);
        Assert.Null(catalog.Find("company"));
        Assert.Empty(Assert.IsType<JsonHttpResult<OidcSignInEndpoints.ProviderResponse[]>>(
            OidcSignInEndpoints.Providers(catalog, accounts, security, context)).Value!);
        Assert.IsType<NotFound>(OidcSignInEndpoints.Challenge("company", catalog, accounts, security,
            context, NullLogger<OidcSignInEndpoints>.Instance));
        Assert.Throws<InvalidOperationException>(() => tickets.Issue(identity, "other", "proof"));
        var signIn = Substitute.For<IOidcSignInService>();
        Assert.IsType<NotFound>(await OidcSignInEndpoints.Redeem(new(ticket), tickets, signIn, accounts, security,
            Substitute.For<ISessionCommands>(), Substitute.For<IWallOfShameQueries>(), Substitute.For<ITwoFactorGate>(),
            TimeProvider.System, context, NullLogger<OidcSignInEndpoints>.Instance));
        await signIn.DidNotReceiveWithAnyArgs().Resolve(default!, default);

        configuration["Backend:Accounts:Oidc"] = "true";
        configuration.Reload();
        Assert.True(catalog.IsEnabled);
        Assert.NotNull(catalog.Find("company"));
        Assert.Single(Assert.IsType<JsonHttpResult<OidcSignInEndpoints.ProviderResponse[]>>(
            OidcSignInEndpoints.Providers(catalog, accounts, security, context)).Value!);
    }
}
