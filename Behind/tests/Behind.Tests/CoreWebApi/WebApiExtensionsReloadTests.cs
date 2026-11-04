using Behind.Common.Configuration;
using Behind.Common.Storage;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi;
using Behind.Core.WebApi.EmailSender;
using Behind.Core.WebApi.Settings;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi;

public sealed class WebApiExtensionsReloadTests : IAsyncLifetime
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"behind-reload-{Guid.NewGuid():N}");
    private WebApplication _app = null!;

    private string PathOf(string name) => Path.Combine(_contentRoot, name);

    private string DataDirectory(string name) => Path.Combine(_contentRoot, name);

    private static string BackendJson(string directory) =>
        $$"""{ "Backend": { "DataDirectory": "{{directory.Replace("\\", "\\\\")}}" } }""";

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_contentRoot);
        await File.WriteAllTextAsync(
            PathOf("database.json"),
            $$"""
              {
                "ConnectionStrings": { "Default": "Data Source={{PathOf("one.db")}}", "Other": "Data Source={{PathOf("two.db")}}" },
                "Database": { "Provider": "Sqlite", "Key": "Default" }
              }
              """);
        await File.WriteAllTextAsync(PathOf("email.json"), "{}");
        await File.WriteAllTextAsync(PathOf("backend.json"), BackendJson(DataDirectory("data-one")));
        await File.WriteAllTextAsync(PathOf("security.json"), "{}");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = _contentRoot });
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        builder.AddCore();
        _app = builder.Build();
        await _app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_contentRoot, true);
    }

    private T Current<T>() where T : class => _app.Services.GetRequiredService<IOptions<T>>().Value;

    [Fact]
    public async Task Email_AfterEmail_json_Changes_UsesTheNewProviderWithoutARestart()
    {
        Assert.IsType<LoggerEmailSender>(_app.Services.GetRequiredService<IEmailSender>());

        await new FileInfo(PathOf("email.json")).WriteEmailConfigurationAsync(
            new SmtpEmailConfiguration("smtp.example.com", 587, new Ssl(true), Some<Authentication>(new NoAuthentication()), None));

        var sender = await Wait.UntilAsync(
            () => _app.Services.GetRequiredService<IEmailSender>(),
            current => current is SmtpEmailSender);
        Assert.IsType<SmtpEmailSender>(sender);
    }

    [Fact]
    public async Task Security_AfterSecurity_json_Changes_ServesTheNewSettings()
    {
        Assert.True(Current<SecurityConfiguration>().Jwt.Match(() => true, _ => false));

        await File.WriteAllTextAsync(
            PathOf("security.json"),
            """{ "Security": { "EmailConfirmation": false, "Jwt": { "Issuer": "issuer-two", "Audience": "audience", "SigningKey": "0123456789abcdef0123456789abcdef" } } }""");

        var security = await Wait.UntilAsync(() => Current<SecurityConfiguration>(), current => !current.EmailConfirmation);
        Assert.Equal("issuer-two", security.Jwt.Match(() => string.Empty, jwt => jwt.Issuer));
    }

    [Fact]
    public async Task Jwt_AfterSecurity_json_Changes_ValidatesWithTheNewSettings()
    {
        var monitor = _app.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        Assert.Null(monitor.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters.ValidIssuer);

        await File.WriteAllTextAsync(
            PathOf("security.json"),
            """{ "Security": { "Jwt": { "Issuer": "issuer-three", "Audience": "audience", "SigningKey": "0123456789abcdef0123456789abcdef" } } }""");

        var issuer = await Wait.UntilAsync(
            () => monitor.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters.ValidIssuer,
            current => current == "issuer-three");
        Assert.Equal("issuer-three", issuer);
    }

    [Fact]
    public async Task Backend_AfterBackend_json_Changes_StoresContentInTheNewDataDirectory()
    {
        await File.WriteAllTextAsync(PathOf("backend.json"), BackendJson(DataDirectory("data-two")));

        await Wait.UntilAsync(() => Current<BackendConfiguration>().DataDirectory, current => current == DataDirectory("data-two"));
        await _app.Services.GetRequiredService<IContentStore>().Write("Sites/file", [1]);

        Assert.True(File.Exists(Path.Combine(DataDirectory("data-two"), "vfs", "Sites", "file")));
    }

    [Fact]
    public async Task Backend_AfterBackend_json_LosesItsDataDirectory_KeepsThePreviousOne()
    {
        await File.WriteAllTextAsync(PathOf("backend.json"), """{ "Backend": { } }""");
        await Task.Delay(500);

        Assert.Equal(DataDirectory("data-one"), Current<BackendConfiguration>().DataDirectory);
    }

    [Fact]
    public async Task Database_AfterDatabase_json_Changes_UsesTheNewConnection()
    {
        await File.WriteAllTextAsync(
            PathOf("database.json"),
            $$"""
              {
                "ConnectionStrings": { "Default": "Data Source={{PathOf("one.db")}}", "Other": "Data Source={{PathOf("two.db")}}" },
                "Database": { "Provider": "Sqlite", "Key": "Other" }
              }
              """);

        await Wait.UntilAsync(
            () => Current<DatabaseConfiguration>().Key.Match(() => string.Empty, key => key.Value),
            current => current == "Other");
        await using var scope = _app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Behind.Core.Database.AbstractCoreContext>();

        Assert.Contains("two.db", context.Database.GetConnectionString());
    }

    [Fact]
    public async Task Frontend_AfterFrontend_json_Changes_ServesTheNewStaticFileOptions()
    {
        await File.WriteAllTextAsync(PathOf("frontend.json"), """{ "Frontend": { "StaticFiles": { "ReferrerPolicy": "origin" } } }""");

        var options = await Wait.UntilAsync(() => Current<FrontendOptions>(), current => current.ReferrerPolicy == "origin");
        Assert.Equal("origin", options.ReferrerPolicy);
    }

    [Fact]
    public async Task Email_AfterEmail_json_BecomesInvalid_KeepsThePreviousProvider()
    {
        await File.WriteAllTextAsync(PathOf("email.json"), "{ not json");
        await Task.Delay(500);

        Assert.IsType<LoggerEmailSender>(_app.Services.GetRequiredService<IEmailSender>());
    }
}
