using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common.WebApi.Filters;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.MagicLinks;
using Behind.Core.WebApi.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class SettingsEndpointsHttpTests : IAsyncLifetime
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;
    private WebApplication _application = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        File.WriteAllText(
            Path.Combine(_directory, "backend.json"),
            """{ "Backend": { "Comments": { "News": true }, "Downloads": { "MaxFileBytes": 100 } } }""");
        File.WriteAllText(
            Path.Combine(_directory, "appsettings.json"),
            """{ "Serilog": { "MinimumLevel": { "Default": "Information" } } }""");
        File.WriteAllText(Path.Combine(_directory, "email.json"), """{ "Email": { "Provider": "Logger" } }""");
        File.WriteAllText(
            Path.Combine(_directory, "security.json"),
            """{ "Security": { "Jwt": { "Issuer": "me", "Audience": "you", "SigningKey": "signing-key-value" } } }""");

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(ConfigurationFileCatalog.For(_directory));
        builder.Services.AddSingleton(TestLocales.Embedded);
        builder.Services.AddSingleton(Options.Create(new SecurityConfiguration(false, None, None, None)));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IMagicLinkSettingsStore>(new MagicLinkSettingsStore(None));
        builder.Services.AddSingleton<IMagicLinkSettingsPersister, NoOpMagicLinkSettingsPersister>();
        builder.Services.AddSingleton<MagicLinkSecretRotator>();

        _application = builder.Build();
        _application.MapGet("/settings", SettingsEndpoints.Get);
        _application.MapGet("/settings/{file}/logging", SettingsEndpoints.GetLogging);
        _application.MapGet("/settings/{file}/opentelemetry", SettingsEndpoints.GetOpenTelemetry);
        _application.MapGet("/settings/forwarded-headers", SettingsEndpoints.GetForwardedHeaders);
        _application.MapPut("/settings/forwarded-headers", SettingsEndpoints.UpdateForwardedHeaders);
        _application.MapGet("/settings/email", SettingsEndpoints.GetEmail);
        _application.MapPut("/settings/email", SettingsEndpoints.UpdateEmail)
            .AddEndpointFilter<DataAnnotationValidationFilter<EmailSettingsRequest>>();
        _application.MapPut("/settings/{file}", SettingsEndpoints.Update)
            .AddEndpointFilter<DataAnnotationValidationFilter<SettingsUpdateRequest>>();
        _application.MapPost("/settings/{file}/generate", SettingsEndpoints.Generate)
            .AddEndpointFilter<DataAnnotationValidationFilter<SettingsActionRequest>>();
        await _application.StartAsync();

        var address = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
        Directory.Delete(_directory, true);
    }

    private JsonNode Read(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(_directory, name)))!;

    [Fact]
    public async Task Get_ListsTheFilesAsJson()
    {
        var response = await _client.GetFromJsonAsync<JsonNode>("/settings");

        Assert.False((bool)response!["readOnly"]!);
        Assert.Equal(["appsettings.json", "backend.json", "email.json", "security.json"], response["files"]!.AsArray().Select(file => (string)file!["name"]!));
        var signing = response["files"]![3]!["fields"]!.AsArray()
            .Single(field => (string)field!["path"]! == "Security:Jwt:SigningKey")!;
        Assert.True((bool)signing["sensitive"]!);
        Assert.Equal("generate", (string)signing["action"]!);
    }

    [Fact]
    public async Task ForwardedHeaders_StartDisabledAndPersistOnlyThroughValidatedEditor()
    {
        const string route = "/settings/forwarded-headers";
        var defaults = await _client.GetFromJsonAsync<ForwardedHeadersConfiguration>(route);
        Assert.False(defaults!.Enabled);
        Assert.Empty(defaults.KnownForwarders);
        Assert.Equal(1, defaults.ForwardLimit);

        var update = new ForwardedHeadersSettings(true, ["192.0.2.8"], 2);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsJsonAsync(route, update)).StatusCode);
        var persisted = await _client.GetFromJsonAsync<ForwardedHeadersConfiguration>(route);
        Assert.True(persisted!.Enabled);
        Assert.Equal(update.ForwardLimit, persisted.ForwardLimit);
        Assert.Equal(update.KnownForwarders, persisted.KnownForwarders);
        Assert.True((bool)Read("backend.json")["Backend"]!["Comments"]!["News"]!);

        var fields = await _client.GetFromJsonAsync<SettingsResponse>("/settings");
        Assert.DoesNotContain(fields!.Files.Single(file => file.Name == "backend.json").Fields,
            field => ForwardedHeadersSettings.IsManagedPath(field.Path));

        var before = Read("backend.json").ToJsonString();
        var generic = new { values = new Dictionary<string, object> { ["Backend:ForwardedHeaders:KnownForwarders:0"] = "::/0" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync("/settings/backend.json", generic)).StatusCode);
        foreach (var invalid in new[] { update with { KnownForwarders = ["0.0.0.0/0"] },
                     update with { KnownForwarders = [] }, update with { ForwardLimit = 0 } })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync(route, invalid)).StatusCode);
        }
        Assert.Equal(before, Read("backend.json").ToJsonString());

        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsJsonAsync(route, update with { Enabled = false })).StatusCode);
        var disabled = await _client.GetFromJsonAsync<ForwardedHeadersConfiguration>(route);
        Assert.False(disabled!.Enabled);
        Assert.Equal(update.KnownForwarders, disabled.KnownForwarders);
        Assert.Equal(update.ForwardLimit, disabled.ForwardLimit);
    }

    [Theory]
    [InlineData("""
        {
          "Backend": { "Comments": { "News": true } },
          "backend:forwardedheaders": { "Enabled": true, "KnownForwarders": ["192.0.2.8"], "ForwardLimit": 2 }
        }
        """)]
    [InlineData("""
        {
          "Backend": { "Comments": { "News": true }, "forwardedheaders:knownforwarders:0": "192.0.2.8" },
          "Backend:ForwardedHeaders:Enabled": true,
          "Backend:ForwardedHeaders:ForwardLimit": 2
        }
        """)]
    public async Task ForwardedHeaders_ReadsFlatKeysAndPersistsOneNestedSection(string json)
    {
        const string route = "/settings/forwarded-headers";
        var path = Path.Combine(_directory, "backend.json");
        await File.WriteAllTextAsync(path, json);

        var settings = await _client.GetFromJsonAsync<ForwardedHeadersConfiguration>(route);
        Assert.True(settings!.Enabled);
        Assert.Equal(["192.0.2.8"], settings.KnownForwarders);
        Assert.Equal(2, settings.ForwardLimit);

        var update = new ForwardedHeadersSettings(true, ["192.0.2.9"], 3);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsJsonAsync(route, update)).StatusCode);
        var root = Read("backend.json").AsObject();
        Assert.DoesNotContain(root, property => ForwardedHeadersSettings.IsManagedPath(property.Key));
        var backend = root["Backend"]!.AsObject();
        Assert.Equal(["Comments", "ForwardedHeaders"], backend.Select(property => property.Key).Order());
        Assert.True((bool)backend["Comments"]!["News"]!);

        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(path);
        var persisted = configuration.GetSection(ForwardedHeadersConfiguration.SectionName)
            .Get<ForwardedHeadersConfiguration>();
        Assert.True(persisted!.Enabled);
        Assert.Equal(update.KnownForwarders, persisted.KnownForwarders);
        Assert.Equal(update.ForwardLimit, persisted.ForwardLimit);
    }

    [Fact]
    public async Task ForwardedHeaders_ReadOnlyConfigurationRejectsWrites()
    {
        var before = Read("backend.json").ToJsonString();
        var result = await SettingsEndpoints.UpdateForwardedHeaders(
            new ForwardedHeadersSettings(true, ["192.0.2.8"], 1), ConfigurationFileCatalog.For(_directory),
            Options.Create(new SecurityConfiguration(true, None, None, None)), NullLogger<SettingsEndpoints>.Instance,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(before, Read("backend.json").ToJsonString());
    }

    [Fact]
    public async Task Put_BindsTypedValuesFromTheBody()
    {
        var response = await _client.PutAsJsonAsync(
            "/settings/backend.json",
            new { values = new Dictionary<string, object> { ["Backend:Comments:News"] = false, ["Backend:Downloads:MaxFileBytes"] = 250 } });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var backend = Read("backend.json")["Backend"]!;
        Assert.False((bool)backend["Comments"]!["News"]!);
        Assert.Equal(250, (int)backend["Downloads"]!["MaxFileBytes"]!);
    }

    [Fact]
    public async Task PasswordSignInSwitch_DisplaysAndPersistsBothStatesWithoutExposingPasswords()
    {
        File.WriteAllText(Path.Combine(_directory, "backend.json"),
            """{"Backend":{"Accounts":{"Password":false},"Database":{"Password":"actual-password"}}}""");

        async Task<JsonNode> Field(string path)
        {
            var response = await _client.GetFromJsonAsync<JsonNode>("/settings");
            return response!["files"]!.AsArray()
                .Single(file => (string)file!["name"]! == "backend.json")!["fields"]!.AsArray()
                .Single(field => (string)field!["path"]! == path)!;
        }

        async Task AssertSwitch(bool expected)
        {
            var field = await Field("Backend:Accounts:Password");
            Assert.Equal("boolean", (string)field["type"]!);
            Assert.Equal(expected ? "true" : "false", (string)field["value"]!);
            Assert.False((bool)field["sensitive"]!);
            Assert.True((bool)field["editable"]!);
        }

        await AssertSwitch(false);
        var secret = await Field("Backend:Database:Password");
        Assert.True((bool)secret["sensitive"]!);
        Assert.DoesNotContain("actual-password", (string)secret["value"]!);

        foreach (var enabled in new[] { true, false })
        {
            var update = await _client.PutAsJsonAsync("/settings/backend.json",
                new { values = new Dictionary<string, object> { ["Backend:Accounts:Password"] = enabled } });
            Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
            Assert.Equal(enabled, (bool)Read("backend.json")["Backend"]!["Accounts"]!["Password"]!);
            await AssertSwitch(enabled);
        }
    }

    [Fact]
    public async Task PasswordSignInSwitch_InvalidStringRemainsMasked()
    {
        File.WriteAllText(Path.Combine(_directory, "backend.json"),
            """{"Backend":{"Accounts":{"Password":"invalid-password"}}}""");
        var response = await _client.GetFromJsonAsync<JsonNode>("/settings");
        var field = response!["files"]!.AsArray()
            .Single(file => (string)file!["name"]! == "backend.json")!["fields"]!.AsArray()
            .Single(field => (string)field!["path"]! == "Backend:Accounts:Password")!;

        Assert.True((bool)field["sensitive"]!);
        Assert.DoesNotContain("invalid-password", (string)field["value"]!);
    }

    [Fact]
    public async Task Put_WithoutValues_IsABadRequest()
    {
        var response = await _client.PutAsJsonAsync("/settings/backend.json", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithAWrongType_IsABadRequest()
    {
        var response = await _client.PutAsJsonAsync(
            "/settings/backend.json",
            new { values = new Dictionary<string, object> { ["Backend:Comments:News"] = "yes" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_ReplacesTheKeyAndNeverEchoesIt()
    {
        var response = await _client.PostAsJsonAsync(
            "/settings/security.json/generate",
            new { path = "Security:Jwt:SigningKey" });

        var body = await response.Content.ReadAsStringAsync();
        var key = (string)Read("security.json")["Security"]!["Jwt"]!["SigningKey"]!;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual("signing-key-value", key);
        Assert.DoesNotContain(key, body);
        Assert.Equal("Security:Jwt:SigningKey", JsonDocument.Parse(body).RootElement.GetProperty("path").GetString());
    }

    [Fact]
    public async Task Generate_WithoutAPath_IsABadRequest()
    {
        var response = await _client.PostAsJsonAsync("/settings/security.json/generate", new { path = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutEmail_BindsTheSmtpRequestAndGetEmailReadsItBack()
    {
        var response = await _client.PutAsJsonAsync(
            "/settings/email",
            new
            {
                provider = "Smtp",
                smtp = new
                {
                    server = "smtp.example.com",
                    port = 2525,
                    ssl = true,
                    from = "noreply@example.com",
                    authentication = "Basic",
                    username = "user",
                    password = "pass",
                    templates = new Dictionary<string, string> { ["MagicLinkEmail"] = "<p>x</p>" }
                }
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var settings = await _client.GetFromJsonAsync<JsonNode>("/settings/email");
        Assert.Equal("Smtp", (string)settings!["provider"]!);
        Assert.Equal(2525, (int)settings["smtp"]!["port"]!);
        Assert.Equal("user", (string)settings["smtp"]!["username"]!);
        Assert.Equal("<p>x</p>", (string)settings["smtp"]!["templates"]!["MagicLinkEmail"]!);
        Assert.DoesNotContain("\"pass\"", settings.ToJsonString());
        Assert.Equal("pass", (string)Read("email.json")["Email"]!["Authentication"]!["Password"]!);
    }

    [Fact]
    public async Task PutEmail_WithoutAProvider_IsABadRequest()
    {
        var response = await _client.PutAsJsonAsync("/settings/email", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutEmail_WithAnInvalidPort_IsABadRequest()
    {
        var response = await _client.PutAsJsonAsync(
            "/settings/email",
            new { provider = "Smtp", smtp = new { server = "x", port = 0, authentication = "No" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/settings/appsettings.json/logging")]
    [InlineData("/settings/appsettings.json/opentelemetry")]
    public async Task PutAppSettings_UsesOneUpdateEndpoint(string oldEndpoint)
    {
        var response = await _client.PutAsJsonAsync(oldEndpoint, new { });

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task PutAppSettings_UpdatesLoggingAndOpenTelemetryTogether()
    {
        var response = await _client.PutAsJsonAsync(
            "/settings/appsettings.json",
            new
            {
                values = new { },
                logging = new
                {
                    @default = "Warning",
                    overrides = new[] { new { source = "Behind", level = "Debug" } }
                },
                openTelemetry = new
                {
                    serviceName = "test-service",
                    isEnabled = true,
                    traces = new
                    {
                        isEnabled = true,
                        endpoint = "https://otel.example/traces",
                        protocol = "HttpProtobuf",
                        instruments = new[] { "AspNetCoreInstrumentation", "OtlpExporter" }
                    },
                    metrics = new { isEnabled = false, endpoint = (string?)null, protocol = (string?)null, instruments = Array.Empty<string>() },
                    logging = new
                    {
                        isEnabled = false,
                        endpoint = (string?)null,
                        protocol = (string?)null,
                        instruments = Array.Empty<string>(),
                        includeFormattedMessage = (bool?)null,
                        includeScopes = (bool?)null,
                        parseStateValues = (bool?)null
                    }
                }
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var settings = await _client.GetFromJsonAsync<JsonNode>("/settings/appsettings.json/opentelemetry");
        Assert.Equal("test-service", (string)settings!["serviceName"]!);
        Assert.Equal("HttpProtobuf", (string)settings["traces"]!["protocol"]!);
        Assert.Equal("OtlpExporter", (string)settings["traces"]!["instruments"]![1]!);
        Assert.Null(settings["logging"]!["includeScopes"]);

        var appSettings = Read("appsettings.json");
        Assert.Equal("test-service", (string)appSettings["OpenTelemetry"]!["ServiceName"]!);
        Assert.Equal("Warning", (string)appSettings["Serilog"]!["MinimumLevel"]!["Default"]!);
        Assert.Equal("Debug", (string)appSettings["Serilog"]!["MinimumLevel"]!["Override"]!["Behind"]!);
    }

    [Fact]
    public async Task PutAppSettings_WithMissingExporterEndpointDoesNotSaveEitherSection()
    {
        var before = File.ReadAllText(Path.Combine(_directory, "appsettings.json"));
        var response = await _client.PutAsJsonAsync(
            "/settings/appsettings.json",
            new
            {
                values = new { },
                logging = new { @default = "Warning", overrides = Array.Empty<object>() },
                openTelemetry = new
                {
                    serviceName = "test-service",
                    isEnabled = true,
                    traces = new { isEnabled = true, endpoint = (string?)null, protocol = "HttpProtobuf", instruments = new[] { "OtlpExporter" } },
                    metrics = new { isEnabled = false, endpoint = (string?)null, protocol = (string?)null, instruments = Array.Empty<string>() },
                    logging = new { isEnabled = false, endpoint = (string?)null, protocol = (string?)null, instruments = Array.Empty<string>() }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, File.ReadAllText(Path.Combine(_directory, "appsettings.json")));
    }
}
