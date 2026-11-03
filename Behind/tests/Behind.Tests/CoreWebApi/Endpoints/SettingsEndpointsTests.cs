using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.MagicLinks;
using Behind.Core.WebApi.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class SettingsEndpointsTests : IDisposable
{
    private const string SecurityJson =
        """
        {
          "Security": {
            "SecretKey": "totp://YO7XF76XLQKJOMQ4FDAQXAESBI2CVOUL",
            "Jwt": { "Issuer": "Behind Backend", "Audience": "Frontend", "SigningKey": "hpFPS7Uz7BHTSWyZylBbjxnHlAU3FoIa" },
            "Elevation": { "Enabled": true },
            "MagicLink": { "ActualSecret": "uuid://01a19375-e2a2-7b3e-a484-7cc04013c5f6" }
          }
        }
        """;

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;
    private readonly ILogger<SettingsEndpoints> _logger = NullLogger<SettingsEndpoints>.Instance;

    public SettingsEndpointsTests()
    {
        File.WriteAllText(PathOf("security.json"), SecurityJson);
        File.WriteAllText(PathOf("backend.json"), """{ "Backend": { "Comments": { "News": true }, "Downloads": { "MaxFileBytes": 100 } } }""");
    }

    public void Dispose() => Directory.Delete(_directory, true);

    private string PathOf(string name) => Path.Combine(_directory, name);

    private ConfigurationFileCatalog Catalog => ConfigurationFileCatalog.For(_directory);

    private static IOptions<SecurityConfiguration> Security(bool readOnly = false) =>
        Options.Create(new SecurityConfiguration(readOnly, None, None, None));

    private JsonNode Read(string name) => JsonNode.Parse(File.ReadAllText(PathOf(name)))!;

    private static SettingsUpdateRequest Update(params (string Path, string Json)[] pairs) =>
        new(pairs.ToDictionary(pair => pair.Path, pair => JsonDocument.Parse(pair.Json).RootElement.Clone()));

    private MagicLinkSecretRotator Rotator(
        out MagicLinkSettingsStore store,
        Option<MagicLinkSettings> settings = default)
    {
        store = new MagicLinkSettingsStore(settings);

        return new MagicLinkSecretRotator(
            store,
            new JsonFileMagicLinkSettingsPersister(new FileInfo(PathOf("security.json"))),
            new FixedTimeProvider(Now),
            Substitute.For<ILogger<MagicLinkSecretRotator>>());
    }

    private static SettingsFieldResponse Field(SettingsResponse response, string file, string path) =>
        response.Files.Single(found => found.Name == file).Fields.Single(field => field.Path == path);

    private async Task<SettingsResponse> Listed(bool readOnly = false) =>
        Assert.IsType<JsonHttpResult<SettingsResponse>>(
            await SettingsEndpoints.Get(Catalog, Security(readOnly), CancellationToken.None)).Value!;

    [Fact]
    public async Task Get_ListsEveryExistingFileAsATab()
    {
        var response = await Listed();

        Assert.Equal(["backend.json", "security.json"], response.Files.Select(file => file.Name));
        Assert.False(response.ReadOnly);
    }

    [Fact]
    public async Task Get_ReportsAReadOnlyConfiguration()
    {
        Assert.True((await Listed(readOnly: true)).ReadOnly);
    }

    [Fact]
    public async Task Get_MasksEverySensitiveValue()
    {
        var response = await Listed();

        var secret = Field(response, "security.json", "Security:SecretKey");
        var signing = Field(response, "security.json", "Security:Jwt:SigningKey");
        var magic = Field(response, "security.json", "Security:MagicLink:ActualSecret");

        Assert.All([secret, signing, magic], field => Assert.True(field.Sensitive));
        Assert.Equal("totp://YO7XF76XLQKJOMQ4FDAQXAESBI2CVOUL".MaskSensitiveValue(), secret.Value);
        Assert.DoesNotContain("hpFPS7Uz7BHTSWyZylBbjxnHlAU3FoIa", signing.Value);
        Assert.DoesNotContain("01a19375", magic.Value);
        Assert.DoesNotContain("YO7XF76XLQKJOMQ4FDAQXAESBI2CVOUL", JsonSerializer.Serialize(response));
        Assert.DoesNotContain("hpFPS7Uz7BHTSWyZylBbjxnHlAU3FoIa", JsonSerializer.Serialize(response));
    }

    [Fact]
    public async Task Get_ShowsPlainValuesWithTheirTypes()
    {
        var response = await Listed();

        var issuer = Field(response, "security.json", "Security:Jwt:Issuer");
        var enabled = Field(response, "security.json", "Security:Elevation:Enabled");
        var size = Field(response, "backend.json", "Backend:Downloads:MaxFileBytes");

        Assert.Equal(("Behind Backend", "string", false), (issuer.Value, issuer.Type, issuer.Sensitive));
        Assert.Equal(("true", "boolean", false), (enabled.Value, enabled.Type, enabled.Sensitive));
        Assert.Equal(("100", "number", false), (size.Value, size.Type, size.Sensitive));
        Assert.All([issuer, enabled, size], field => Assert.True(field.Editable));
        Assert.All([issuer, enabled, size], field => Assert.Null(field.Action));
    }

    [Fact]
    public async Task Get_OffersGenerateAndRotateOnTheSecretsAndLocksTheirValues()
    {
        var response = await Listed();

        Assert.Equal("generate", Field(response, "security.json", "Security:SecretKey").Action);
        Assert.Equal("generate", Field(response, "security.json", "Security:Jwt:SigningKey").Action);
        Assert.Equal("rotate", Field(response, "security.json", "Security:MagicLink:ActualSecret").Action);
        Assert.All(
            response.Files.Single(file => file.Name == "security.json").Fields.Where(field => field.Action is not null),
            field => Assert.False(field.Editable));
    }

    [Fact]
    public async Task Get_OffersGeneratingAMissingDeveloperSecretKey()
    {
        var field = Field(await Listed(), "security.json", "Security:Elevation:DeveloperSecretKey");

        Assert.Equal(string.Empty, field.Value);
        Assert.Equal("generate", field.Action);
        Assert.True(field.Sensitive);
        Assert.False(field.Editable);
    }

    [Fact]
    public async Task Update_WritesTypedValuesAndKeepsTheRest()
    {
        var result = await SettingsEndpoints.Update(
            "backend.json",
            Update(("Backend:Comments:News", "false"), ("Backend:Downloads:MaxFileBytes", "200")),
            Catalog,
            Security(),
            _logger,
            CancellationToken.None);

        Assert.IsType<NoContent>(result);
        var backend = Read("backend.json")["Backend"]!;
        Assert.False((bool?)backend["Comments"]!["News"]);
        Assert.Equal(200, (int?)backend["Downloads"]!["MaxFileBytes"]);
    }

    [Fact]
    public async Task Update_ReplacesASensitiveValueThatHasNoAction()
    {
        File.WriteAllText(PathOf("database.json"), """{ "ConnectionStrings": { "Default": "Data Source=a.db" } }""");

        var result = await SettingsEndpoints.Update(
            "database.json",
            Update(("ConnectionStrings:Default", "\"Data Source=b.db\"")),
            Catalog,
            Security(),
            _logger,
            CancellationToken.None);

        Assert.IsType<NoContent>(result);
        Assert.Equal("Data Source=b.db", (string?)Read("database.json")["ConnectionStrings"]!["Default"]);
    }

    [Theory]
    [InlineData("Security:Jwt:SigningKey", "\"typed\"")]
    [InlineData("Security:Jwt:Issuer", "5")]
    [InlineData("Security:Jwt:Missing", "\"x\"")]
    public async Task Update_RefusesInvalidChangesAndWritesNothing(string path, string json)
    {
        var before = File.ReadAllText(PathOf("security.json"));

        var result = await SettingsEndpoints.Update(
            "security.json",
            Update(("Security:Jwt:Audience", "\"changed\""), (path, json)),
            Catalog,
            Security(),
            _logger,
            CancellationToken.None);

        Assert.IsType<BadRequest>(result);
        Assert.Equal(before, File.ReadAllText(PathOf("security.json")));
    }

    [Fact]
    public async Task Update_UnknownFile_IsNotFound()
    {
        var result = await SettingsEndpoints.Update(
            "../secret.json",
            Update(("A", "\"b\"")),
            Catalog,
            Security(),
            _logger,
            CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Update_ReadOnlyConfiguration_IsForbidden()
    {
        var before = File.ReadAllText(PathOf("backend.json"));

        var result = await SettingsEndpoints.Update(
            "backend.json",
            Update(("Backend:Comments:News", "false")),
            Catalog,
            Security(readOnly: true),
            _logger,
            CancellationToken.None);

        var forbidden = Assert.IsType<JsonHttpResult<ConfigurationForbiddenResponse>>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Equal(ConfigurationForbiddenResponse.ConfigurationReadOnly, forbidden.Value);
        Assert.Equal(before, File.ReadAllText(PathOf("backend.json")));
    }

    [Fact]
    public async Task Generate_ReplacesTheJwtSigningKeyWithoutRevealingIt()
    {
        var result = await SettingsEndpoints.Generate(
            "security.json",
            new SettingsActionRequest("Security:Jwt:SigningKey"),
            Catalog,
            Security(),
            _logger,
            CancellationToken.None);

        var response = Assert.IsType<JsonHttpResult<SettingsActionResponse>>(result).Value!;
        var key = (string?)Read("security.json")["Security"]!["Jwt"]!["SigningKey"];
        Assert.Equal(64, key!.Length);
        Assert.NotEqual("hpFPS7Uz7BHTSWyZylBbjxnHlAU3FoIa", key);
        Assert.Null(response.OtpUri);
        Assert.DoesNotContain(key, JsonSerializer.Serialize(response));
        Assert.Equal("Behind Backend", (string?)Read("security.json")["Security"]!["Jwt"]!["Issuer"]);
    }

    [Fact]
    public async Task Generate_CreatesATotpSecretAndAnswersTheAuthenticatorUriOnce()
    {
        var result = await SettingsEndpoints.Generate(
            "security.json",
            new SettingsActionRequest("Security:SecretKey"),
            Catalog,
            Security(),
            _logger,
            CancellationToken.None);

        var response = Assert.IsType<JsonHttpResult<SettingsActionResponse>>(result).Value!;
        var value = (string?)Read("security.json")["Security"]!["SecretKey"];
        Assert.StartsWith("totp://", value);
        Assert.NotEqual("totp://YO7XF76XLQKJOMQ4FDAQXAESBI2CVOUL", value);
        Assert.Contains(value!["totp://".Length..].TrimEnd('='), response.OtpUri);
        Assert.StartsWith("otpauth://totp/", response.OtpUri);
    }

    [Fact]
    public async Task Generate_CreatesAMissingDeveloperSecretKey()
    {
        var result = await SettingsEndpoints.Generate(
            "security.json",
            new SettingsActionRequest("Security:Elevation:DeveloperSecretKey"),
            Catalog,
            Security(),
            _logger,
            CancellationToken.None);

        Assert.IsType<JsonHttpResult<SettingsActionResponse>>(result);
        var elevation = Read("security.json")["Security"]!["Elevation"]!;
        Assert.StartsWith("totp://", (string?)elevation["DeveloperSecretKey"]);
        Assert.True((bool?)elevation["Enabled"]);
    }

    [Fact]
    public async Task Generate_MissingValueWithoutCreation_IsNotFound()
    {
        File.WriteAllText(PathOf("security.json"), """{ "Security": { "Jwt": { "Issuer": "x" } } }""");

        var result = await SettingsEndpoints.Generate(
            "security.json",
            new SettingsActionRequest("Security:Jwt:SigningKey"),
            Catalog,
            Security(),
            _logger,
            CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Theory]
    [InlineData("security.json", "Security:Jwt:Issuer")]
    [InlineData("security.json", "Security:MagicLink:ActualSecret")]
    [InlineData("backend.json", "Security:SecretKey")]
    public async Task Generate_RefusesWhatIsNotGeneratable(string file, string path)
    {
        var result = await SettingsEndpoints.Generate(
            file,
            new SettingsActionRequest(path),
            Catalog,
            Security(),
            _logger,
            CancellationToken.None);

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Generate_UnknownFile_IsNotFound()
    {
        var result = await SettingsEndpoints.Generate(
            "unknown.json",
            new SettingsActionRequest("Security:SecretKey"),
            Catalog,
            Security(),
            _logger,
            CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Generate_ReadOnlyConfiguration_IsForbidden()
    {
        var before = File.ReadAllText(PathOf("security.json"));

        var result = await SettingsEndpoints.Generate(
            "security.json",
            new SettingsActionRequest("Security:SecretKey"),
            Catalog,
            Security(readOnly: true),
            _logger,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<JsonHttpResult<ConfigurationForbiddenResponse>>(result).StatusCode);
        Assert.Equal(before, File.ReadAllText(PathOf("security.json")));
    }

    [Fact]
    public async Task Rotate_ReplacesTheMagicLinkSecretAndKeepsTheOldOne()
    {
        Uuid.TryCreate("01a19375-e2a2-7b3e-a484-7cc04013c5f6", out var actual);
        var rotator = Rotator(out var store, Some(new MagicLinkSettings(actual, None)));

        var result = await SettingsEndpoints.Rotate(
            "security.json",
            new SettingsActionRequest("Security:MagicLink:ActualSecret"),
            Catalog,
            Security(),
            rotator,
            _logger,
            CancellationToken.None);

        var response = Assert.IsType<JsonHttpResult<SettingsActionResponse>>(result).Value!;
        var section = Read("security.json")["Security"]!["MagicLink"]!;
        Assert.Equal("Security:MagicLink:ActualSecret", response.Path);
        Assert.Null(response.OtpUri);
        Assert.Equal("uuid://01a19375-e2a2-7b3e-a484-7cc04013c5f6", (string?)section["OldSecret"]);
        Assert.NotEqual("uuid://01a19375-e2a2-7b3e-a484-7cc04013c5f6", (string?)section["ActualSecret"]);
        Assert.Equal(
            Some(Now),
            store.Current.Bind(settings => settings.ActualSecret.CreatedAt));
    }

    [Fact]
    public async Task Rotate_WithoutMagicLinks_IsNotFound()
    {
        var result = await SettingsEndpoints.Rotate(
            "security.json",
            new SettingsActionRequest("Security:MagicLink:ActualSecret"),
            Catalog,
            Security(),
            Rotator(out _),
            _logger,
            CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Theory]
    [InlineData("security.json", "Security:Jwt:SigningKey")]
    [InlineData("security.json", "Security:Jwt:Issuer")]
    [InlineData("backend.json", "Security:MagicLink:ActualSecret")]
    public async Task Rotate_RefusesWhatIsNotRotatable(string file, string path)
    {
        var result = await SettingsEndpoints.Rotate(
            file,
            new SettingsActionRequest(path),
            Catalog,
            Security(),
            Rotator(out _),
            _logger,
            CancellationToken.None);

        Assert.IsType<BadRequest>(result);
    }

    [Fact]
    public async Task Rotate_ReadOnlyConfiguration_IsForbidden()
    {
        var result = await SettingsEndpoints.Rotate(
            "security.json",
            new SettingsActionRequest("Security:MagicLink:ActualSecret"),
            Catalog,
            Security(readOnly: true),
            Rotator(out _),
            _logger,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<JsonHttpResult<ConfigurationForbiddenResponse>>(result).StatusCode);
    }
}
