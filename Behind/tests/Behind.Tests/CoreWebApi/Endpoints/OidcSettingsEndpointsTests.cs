using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common.Configuration;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Oidc;
using Behind.Core.WebApi.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class OidcSettingsEndpointsTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("behind-oidc-settings-").FullName;
    private string FilePath => Path.Combine(directory, "security.json");
    private ConfigurationFileCatalog Catalog => ConfigurationFileCatalog.For(directory);
    private static IOptions<SecurityConfiguration> Security(bool readOnly = false) =>
        Options.Create(new SecurityConfiguration(readOnly, None, None, None));
    private static OidcProviderSettings Provider(string id = "company") => new()
    {
        Id = id, DisplayName = id, Authority = $"https://{id}.example.test", ClientId = "behind"
    };

    public OidcSettingsEndpointsTests()
    {
        File.WriteAllText(FilePath,
            """
            {
              "Security": {
                "EmailConfirmation": true,
                "Jwt": {"Issuer": "unchanged"},
                "Oidc": {
                  "UnknownSetting": "preserve",
                  "Providers": [
                    {"Id":"company","DisplayName":"Company","Authority":"https://company.example.test",
                     "ClientId":"behind","ClientSecret":"stored-first-secret","UnknownProviderSetting":"keep-first"},
                    {"Id":"second","DisplayName":"Second","Authority":"https://second.example.test",
                     "ClientId":"behind","ClientSecret":"stored-second-secret","UnknownProviderSetting":"keep-second"}
                  ]
                }
              },
              "OtherSection": {"Retain": true}
            }
            """);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private Task<IResult> Save(OidcSettings settings, bool readOnly = false) =>
        OidcSettingsEndpoints.Update(settings, Catalog, Security(readOnly),
            NullLogger<OidcSettingsEndpoints>.Instance, new DefaultHttpContext(), default);

    private JsonObject Read() => JsonNode.Parse(File.ReadAllText(FilePath))!.AsObject();
    private JsonArray Providers => Read()["Security"]!["Oidc"]!["Providers"]!.AsArray();

    [Fact]
    public async Task ReadNeverReturnsAnyClientSecretAndGenericSecurityEditorExcludesOidc()
    {
        var context = new DefaultHttpContext();
        var response = Assert.IsType<JsonHttpResult<OidcSettings>>(
            await OidcSettingsEndpoints.Get(Catalog, context, default)).Value!;
        Assert.All(response.Providers, provider =>
        {
            Assert.Empty(provider.ClientSecret);
            Assert.True(provider.HasClientSecret);
            Assert.False(provider.ClearClientSecret);
        });
        Assert.DoesNotContain("stored-first-secret", JsonSerializer.Serialize(response));
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        var generic = Assert.IsType<JsonHttpResult<SettingsResponse>>(
            await SettingsEndpoints.Get(Catalog, TestLocales.Embedded, Security(), default)).Value!;
        var fields = generic.Files.Single().Fields;
        Assert.DoesNotContain(fields, field => field.Path.StartsWith(OidcConfiguration.SectionName));
        Assert.Contains(fields, field => field.Path == "Security:Jwt:Issuer");
    }

    [Fact]
    public async Task AddRemoveAndReorderProvidersPreservesSecretsByIdAndUnrelatedConfiguration()
    {
        Assert.IsType<NoContent>(await Save(new()
        {
            Providers = [Provider("second"), Provider(), Provider("third")]
        }));
        var providers = Providers;
        Assert.Equal("stored-second-secret", providers[0]!["ClientSecret"]!.GetValue<string>());
        Assert.Equal("stored-first-secret", providers[1]!["ClientSecret"]!.GetValue<string>());
        Assert.Equal(string.Empty, providers[2]!["ClientSecret"]!.GetValue<string>());
        Assert.Equal("keep-second", providers[0]!["UnknownProviderSetting"]!.GetValue<string>());
        Assert.Equal("keep-first", providers[1]!["UnknownProviderSetting"]!.GetValue<string>());
        var root = Read();
        Assert.Equal("preserve", root["Security"]!["Oidc"]!["UnknownSetting"]!.GetValue<string>());
        Assert.True(root["OtherSection"]!["Retain"]!.GetValue<bool>());
        Assert.Equal("unchanged", root["Security"]!["Jwt"]!["Issuer"]!.GetValue<string>());
        Assert.IsType<NoContent>(await Save(new() { Providers = [Provider("second")] }));
        Assert.Single(Providers);
        Assert.Equal("stored-second-secret", Providers[0]!["ClientSecret"]!.GetValue<string>());
    }

    [Fact]
    public async Task SecretsCanBeReplacedOrExplicitlyClearedWithoutSavingUiMetadata()
    {
        Assert.IsType<NoContent>(await Save(new()
        {
            Providers =
            [
                Provider() with { ClientSecret = "new-first-secret" },
                Provider("second") with { ClearClientSecret = true }
            ]
        }));
        Assert.Equal("new-first-secret", Providers[0]!["ClientSecret"]!.GetValue<string>());
        Assert.Empty(Providers[1]!["ClientSecret"]!.GetValue<string>());
        Assert.Null(Providers[0]!["HasClientSecret"]);
        Assert.Null(Providers[1]!["ClearClientSecret"]);
    }

    [Fact]
    public async Task ConfigurationWithoutOidcCanCreateItsFirstProvider()
    {
        File.WriteAllText(FilePath, """{"Security":{"EmailConfirmation":true}}""");
        var current = Assert.IsType<JsonHttpResult<OidcSettings>>(
            await OidcSettingsEndpoints.Get(Catalog, new DefaultHttpContext(), default)).Value!;
        Assert.Empty(current.Providers);
        Assert.IsType<NoContent>(await Save(new() { Providers = [Provider()] }));
        Assert.Equal("company", Assert.Single(Providers)!["Id"]!.GetValue<string>());
        Assert.True(Read()["Security"]!["EmailConfirmation"]!.GetValue<bool>());
    }

    [Fact]
    public async Task SavedConfigurationKeepsExistingPropertyCasing()
    {
        File.WriteAllText(FilePath,
            """
            {"security":{"emailConfirmation":true,"oidc":{"custom":"preserve","providers":[
              {"id":"company","displayName":"Company","authority":"https://company.example.test",
               "clientId":"behind","clientSecret":"stored-secret","custom":"keep"}
            ]}}}
            """);
        Assert.IsType<NoContent>(await Save(new() { Providers = [Provider()] }));
        var root = Read();
        Assert.Null(root["Security"]);
        Assert.Null(root["security"]!["Oidc"]);
        Assert.Equal("preserve", root["security"]!["oidc"]!["custom"]!.GetValue<string>());
        var provider = root["security"]!["oidc"]!["providers"]![0]!;
        Assert.Equal("keep", provider["custom"]!.GetValue<string>());
        Assert.Equal("stored-secret", provider["clientSecret"]!.GetValue<string>());
        Assert.Null(provider["ClientSecret"]);
    }

    [Fact]
    public async Task ReadOnlyAndMissingFileRejectWrites()
    {
        var original = File.ReadAllText(FilePath);
        Assert.Equal(403, Assert.IsType<JsonHttpResult<ConfigurationForbiddenResponse>>(
            await Save(new(), readOnly: true)).StatusCode);
        Assert.Equal(original, File.ReadAllText(FilePath));
        File.Delete(FilePath);
        Assert.IsType<NotFound>(await Save(new()));
        Assert.IsType<NotFound>(await OidcSettingsEndpoints.Get(Catalog, new DefaultHttpContext(), default));
    }

    [Fact]
    public async Task InvalidProviderSettingsAreRejectedWithoutPartialWrites()
    {
        var invalid = new OidcSettings[]
        {
            new() { Providers = [Provider(), Provider("COMPANY")] },
            new() { Providers = [Provider() with { Id = "../bad" }] },
            new() { Providers = [Provider() with { Authority = "http://insecure.example.test" }] },
            new() { Providers = [Provider() with { Scopes = ["email"] }] },
            new() { Providers = [Provider() with { RoleMappings = new() { ["writers"] = [] } }] },
            new() { Providers = [Provider() with { ClientSecret = "conflicting", ClearClientSecret = true }] },
            new() { Providers = [Provider() with { Scopes = null! }] },
            new() { TicketLifetime = TimeSpan.Zero },
            new() { AuthenticationTimeout = TimeSpan.Zero },
            new() { MaxPendingTickets = 0 },
            new() { Providers = null! }
        };
        var original = File.ReadAllText(FilePath);
        foreach (var request in invalid)
        {
            Assert.IsType<BadRequest>(await Save(request));
            Assert.Equal(original, File.ReadAllText(FilePath));
        }
    }

    [Fact]
    public async Task GenericUpdateCannotBypassOidcValidation()
    {
        var original = File.ReadAllText(FilePath);
        var request = new SettingsUpdateRequest(new Dictionary<string, JsonElement>
        {
            ["Security:Oidc:Providers:0:Authority"] = JsonDocument.Parse("\"http://unsafe.example.test\"").RootElement.Clone()
        });
        Assert.IsType<BadRequest>(await SettingsEndpoints.Update("security.json", request, Catalog,
            TestLocales.Embedded, Security(), NullLogger<SettingsEndpoints>.Instance, default));
        Assert.Equal(original, File.ReadAllText(FilePath));
    }

    [Fact]
    public async Task BackendAccountsEditorAddsAndPersistsMissingOidcSwitch()
    {
        var path = Path.Combine(directory, "backend.json");
        File.WriteAllText(path, """{"Backend":{"Accounts":{"Password":true}}}""");
        var response = Assert.IsType<JsonHttpResult<SettingsResponse>>(
            await SettingsEndpoints.Get(Catalog, TestLocales.Embedded, Security(), default)).Value!;
        var toggle = response.Files.Single(file => file.Name == "backend.json").Fields
            .Single(field => field.Path == "Backend:Accounts:Oidc");
        Assert.Equal("boolean", toggle.Type);
        Assert.Equal("true", toggle.Value);
        Assert.True(toggle.Editable);
        var values = new Dictionary<string, JsonElement>
        {
            [toggle.Path] = JsonDocument.Parse("false").RootElement.Clone()
        };
        Assert.IsType<NoContent>(await SettingsEndpoints.Update("backend.json", new(values), Catalog,
            TestLocales.Embedded, Security(), NullLogger<SettingsEndpoints>.Instance, default));
        var written = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.False(written["Backend"]!["Accounts"]!["Oidc"]!.GetValue<bool>());
        Assert.True(written["Backend"]!["Accounts"]!["Password"]!.GetValue<bool>());
    }
}
