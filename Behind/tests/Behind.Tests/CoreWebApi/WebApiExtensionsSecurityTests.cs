using Behind.Core.WebApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_contentRoot, true);
    }

    private WebApplication BuildApplication(string? securityJson)
    {
        if (securityJson is not null)
        {
            File.WriteAllText(Path.Combine(_contentRoot, "security.json"), securityJson);
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = _contentRoot });
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        builder.AddCore();

        return builder.Build();
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
