using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi;

public sealed class WebApiExtensionsSecurityTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"behind-security-{Guid.NewGuid():N}");

    public WebApiExtensionsSecurityTests()
    {
        Directory.CreateDirectory(_contentRoot);
        File.WriteAllText(
            Path.Combine(_contentRoot, "database.json"),
            $$"""
              {
                "ConnectionStrings": { "Default": "Data Source={{Path.Combine(_contentRoot, "test.db")}}" },
                "Database": { "Provider": "Sqlite", "Key": "Default" }
              }
              """);
        File.WriteAllText(Path.Combine(_contentRoot, "email.json"), "{}");
        File.WriteAllText(
            Path.Combine(_contentRoot, "backend.json"),
            $$"""{ "Backend": { "DataDirectory": "{{Path.Combine(_contentRoot, "data").Replace("\\", "\\\\")}}" } }""");
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_contentRoot, true);
    }

    private WebApplication BuildApplication(string? securityJson, bool enableHostForwarding = false)
    {
        if (securityJson is not null)
        {
            File.WriteAllText(Path.Combine(_contentRoot, "security.json"), securityJson);
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = _contentRoot });
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        if (enableHostForwarding)
        {
            builder.Configuration["FORWARDEDHEADERS_ENABLED"] = "true";
        }
        builder.AddCore();

        return builder.Build();
    }

    [Fact]
    public async Task AddCore_HostForwardingSwitchCannotOverrideDynamicClientAddressPolicy()
    {
        await using var app = BuildApplication(null, enableHostForwarding: true);
        var options = app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.False(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
    }

    [Fact]
    public async Task UseCore_WithoutJwtSettings_BuildsThePipeline()
    {
        await using var app = BuildApplication(null);

        var exception = Record.Exception(() => app.UseCore());

        Assert.Null(exception);
    }

    [Fact]
    public async Task AddCore_WithoutJwtSettings_RegistersAuthorizationServices()
    {
        await using var app = BuildApplication(null);

        Assert.NotNull(app.Services.GetService<IAuthorizationService>());
    }

    [Fact]
    public async Task AddCore_UsesCaseSensitiveRoleManagerWithoutChangingUserNormalizer()
    {
        await using var app = BuildApplication(null);
        using var scope = app.Services.CreateScope();
        Assert.IsType<CaseSensitiveRoleManager>(scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>());
        Assert.IsType<UpperInvariantLookupNormalizer>(scope.ServiceProvider.GetRequiredService<ILookupNormalizer>());
    }

    [Fact]
    public async Task UseCore_WithJwtSettings_BuildsThePipeline()
    {
        await using var app = BuildApplication(
            """
            { "Security": { "Jwt": { "Issuer": "behind", "Audience": "behind", "SigningKey": "0123456789abcdef0123456789abcdef" } } }
            """);

        var exception = Record.Exception(() => app.UseCore());

        Assert.Null(exception);
    }

    [Fact]
    public async Task AdultReachedThroughChildPinCannotChangeAccountCredentials()
    {
        const string issuer = "behind";
        const string audience = "behind";
        const string key = "0123456789abcdef0123456789abcdef";
        const string adultPin = "0001";
        await using var app = BuildApplication(
            $$"""
              { "Security": { "Jwt": { "Issuer": "{{issuer}}", "Audience": "{{audience}}", "SigningKey": "{{key}}" } } }
              """);
        var jwt = new JwtSettings(issuer, audience, key);
        string childToken;
        Guid adultId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AbstractCoreContext>();
            await db.Database.MigrateAsync();
            var versioning = scope.ServiceProvider.GetRequiredService<AbstractVersioningContext>();
            await versioning.Database.MigrateAsync();
            await db.EnsureBuiltInUsersAsync();
            await db.EnsureBuiltInRolesAsync(scope.ServiceProvider.GetRequiredService<AccessCatalog>());
            var adult = new HumanUser("Adult", [new Email("adult@example.test")], None);
            var child = new HumanUser("Child", [new Email("child@example.test")], None);
            db.Users.AddRange(adult, child);
            db.Subaccounts.Add(new Subaccount(adult, child, true));
            db.UserSwitchPins.Add(new UserSwitchPin(adult, new PinHash(PasswordHash.Create(adultPin))));
            await db.SaveChangesAsync();
            await db.EnsureUsersHaveUsersRoleAsync();
            var access = AccessToken.Create(child);
            var childId = db.GetKey(child);
            var session = await new SessionCommands(db).CreateForUserId(childId,
                new ValidSession(access, RefreshToken.Create(child), "127.0.0.1", "test"));
            childToken = access.ToJwtToken(child.ToApplicationUser(childId), session, jwt);
            adultId = db.GetKey(adult);
        }
        app.UseCore();
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", childToken);
        app.Configuration["Backend:Accounts:SubaccountSwitch"] = "false";
        using var settings = await client.GetAsync("/accounts/settings");
        Assert.False((await settings.Content.ReadFromJsonAsync<AccountsSettingsResponse>())!.SubaccountSwitch);
        using var familyWhileDisabled = await client.GetAsync("/profile/subaccounts");
        Assert.Equal(HttpStatusCode.OK, familyWhileDisabled.StatusCode);
        using var disabledSwitch = await client.PostAsJsonAsync($"/profile/subaccounts/{adultId}/switch", new { Pin = adultPin });
        Assert.Equal(HttpStatusCode.NotFound, disabledSwitch.StatusCode);

        app.Configuration["Backend:Accounts:SubaccountSwitch"] = "true";
        using var switched = await client.PostAsJsonAsync($"/profile/subaccounts/{adultId}/switch", new { Pin = adultPin });
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        var signIn = Assert.IsType<SignInEndpoints.SignInResponse>(await switched.Content.ReadFromJsonAsync<SignInEndpoints.SignInResponse>());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signIn.AccessToken);

        using var family = await client.GetAsync("/profile/subaccounts");
        Assert.Equal(HttpStatusCode.OK, family.StatusCode);
        using var password = await client.PutAsJsonAsync("/profile/password", new { Password = "test-password" });
        Assert.Equal(HttpStatusCode.Forbidden, password.StatusCode);
        using var email = await client.PostAsJsonAsync("/profile/emails", new { Email = "other@example.test" });
        Assert.Equal(HttpStatusCode.Forbidden, email.StatusCode);
        using var totp = await client.PostAsync("/profile/two-factor/totp", null);
        Assert.Equal(HttpStatusCode.Forbidden, totp.StatusCode);
    }

}
