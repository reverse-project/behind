using System.Security.Claims;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Oidc;
using Behind.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public sealed class OidcTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static OidcProviderConfiguration Provider => new()
    {
        Id = "company", DisplayName = "Company", Authority = "https://id.example.test", ClientId = "behind"
    };
    private static OidcIdentity Identity => new("company", "https://id.example.test", "subject", null, false, null, []);

    [Fact]
    public void ConfigurationBindsMultipleProvidersAndRoleMappings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:Oidc:Providers:0:Id"] = "first",
            ["Security:Oidc:Providers:0:RoleMappings:editors:0"] = "Editor",
            ["Security:Oidc:Providers:0:LinkByVerifiedEmail"] = "true",
            ["Security:Oidc:Providers:1:Id"] = "second",
            ["Security:Oidc:Providers:1:AutoCreateRoles"] = "true"
        }).Build().GetSection(OidcConfiguration.SectionName).Get<OidcConfiguration>()!;

        Assert.Equal(2, configuration.Providers.Length);
        Assert.Equal(["Editor"], configuration.Providers[0].RoleMappings["editors"]);
        Assert.True(configuration.Providers[0].LinkByVerifiedEmail);
        Assert.True(configuration.Providers[1].AutoCreateRoles);
        Assert.Equal(TimeSpan.FromMinutes(2), configuration.TicketLifetime);
    }

    [Fact]
    public void CatalogExcludesDisabledProvidersAndRejectsDuplicateIds()
    {
        Assert.Single(new OidcProviderCatalog(new()
        {
            Providers = [Provider, Provider with { Id = "disabled", Enabled = false }]
        }).Providers);
        Assert.Throws<OptionsValidationException>(() => new OidcProviderCatalog(new()
        {
            Providers = [Provider, Provider with { Id = "COMPANY" }]
        }));
    }

    [Theory]
    [InlineData("http://id.example.test")]
    [InlineData("https://user:password@id.example.test")]
    [InlineData("https://id.example.test?query=1")]
    public void CatalogRequiresSecureAuthority(string authority)
    {
        Assert.Throws<OptionsValidationException>(() => new OidcProviderCatalog(new()
        {
            Providers = [Provider with { Authority = authority }]
        }));
    }

    [Fact]
    public void RegisteredSchemesUseCodePkceAndNeverPersistProviderTokens()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:Url"] = "https://behind.example.test/",
            ["Security:Oidc:Providers:0:Id"] = "first",
            ["Security:Oidc:Providers:0:DisplayName"] = "First",
            ["Security:Oidc:Providers:0:Authority"] = "https://first.example.test",
            ["Security:Oidc:Providers:0:ClientId"] = "behind",
            ["Security:Oidc:Providers:1:Id"] = "second",
            ["Security:Oidc:Providers:1:DisplayName"] = "Second",
            ["Security:Oidc:Providers:1:Authority"] = "https://second.example.test",
            ["Security:Oidc:Providers:1:ClientId"] = "behind"
        });
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.AddOidc();
        using var services = builder.Services.BuildServiceProvider();
        var options = services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>();
        foreach (var id in new[] { "first", "second" })
        {
            var provider = options.Get(OidcProviderCatalog.Scheme(id));
            Assert.Equal("code", provider.ResponseType);
            Assert.True(provider.UsePkce);
            Assert.True(provider.RequireHttpsMetadata);
            Assert.False(provider.MapInboundClaims);
            Assert.False(provider.SaveTokens);
            Assert.Equal(OidcProviderCatalog.CallbackPath(id), provider.CallbackPath.Value);
            Assert.True(provider.ProtocolValidator.RequireNonce);
            Assert.Contains("openid", provider.Scope);
        }
    }

    [Fact]
    public void TicketsAreBrowserBoundAndSingleUse()
    {
        var store = new OidcTicketStore(new(new()), new FixedTimeProvider(Now));
        var code = store.Issue(Identity, "flow", "proof");
        Assert.Null(store.Redeem(code, _ => "wrong"));
        Assert.Null(store.Redeem(code, _ => null));
        var ticket = store.Redeem(code, flow => flow == "flow" ? "proof" : null);
        Assert.NotNull(ticket);
        Assert.Equal(Identity, ticket.Identity);
        Assert.Null(store.Redeem(code, _ => "proof"));
    }

    [Fact]
    public void ExpiredTicketsCannotBeRedeemedAndCapacityIsBounded()
    {
        var clock = new MutableClock(Now);
        var store = new OidcTicketStore(new(new() { MaxPendingTickets = 1 }), clock);
        var code = store.Issue(Identity, "flow", "proof");
        Assert.Throws<InvalidOperationException>(() => store.Issue(Identity, "other", "proof"));
        clock.Now = Now.AddMinutes(2);
        Assert.Null(store.Redeem(code, _ => "proof"));
        Assert.NotEmpty(store.Issue(Identity, "other", "proof"));
    }

    [Fact]
    public async Task ConcurrentRedemptionSucceedsOnlyOnce()
    {
        var store = new OidcTicketStore(new(new()), new FixedTimeProvider(Now));
        var code = store.Issue(Identity, "flow", "proof");
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() => store.Redeem(code, _ => "proof"))));
        Assert.Single(results, result => result is not null);
    }

    [Fact]
    public void ClaimsReadRepeatedAndJsonArrayRolesWithoutTrustingOtherClaims()
    {
        var principal = Principal(new("roles", "editor"), new("roles", "[\"reader\",\"editor\"]"),
            new("email", "user@example.test"), new("email_verified", "true"), new(ClaimTypes.Role, "Administrator"));
        var identity = OidcIdentity.FromPrincipal(Provider, principal);
        Assert.Equal(["editor", "reader"], identity.Roles.Order().ToArray());
        Assert.True(identity.EmailVerified);
        Assert.Equal("subject", identity.Subject);
        Assert.Equal("https://id.example.test", identity.Issuer);
    }

    [Fact]
    public void ClaimsSupportNestedProviderRolePaths()
    {
        var identity = OidcIdentity.FromPrincipal(Provider with { RoleClaimType = "realm_access.roles" },
            Principal(new Claim("realm_access", "{\"roles\":[\"editor\",\"reader\"]}")));
        Assert.Equal(["editor", "reader"], identity.Roles);
    }

    [Fact]
    public void ClaimsRejectMissingSubjectAndMalformedRoles()
    {
        Assert.Throws<InvalidOperationException>(() => OidcIdentity.FromPrincipal(Provider, new(new ClaimsIdentity())));
        Assert.Throws<InvalidOperationException>(() =>
            OidcIdentity.FromPrincipal(Provider, Principal(new Claim("roles", "[42]"))));
        Assert.False(OidcIdentity.FromPrincipal(Provider, Principal(new Claim("email", "user@example.test"))).EmailVerified);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(
        new Claim[] { new("iss", "https://id.example.test"), new("sub", "subject") }.Concat(claims), "oidc"));

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
