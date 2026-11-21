using Behind.Core.WebApi;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
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
}
