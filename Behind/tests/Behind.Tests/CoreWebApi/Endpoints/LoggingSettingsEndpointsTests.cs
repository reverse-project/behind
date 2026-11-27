using System.Text.Json.Nodes;
using System.Text.Json;
using Behind.Common.Configuration;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class LoggingSettingsEndpointsTests : IDisposable
{
    private const string AppSettings = """
        { "Serilog": { "MinimumLevel": { "Default": "Information", "Override": { "Microsoft.AspNetCore": "Warning" } } }, "AllowedHosts": "*" }
        """;

    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;
    private readonly ILogger<SettingsEndpoints> _logger = NullLogger<SettingsEndpoints>.Instance;

    public LoggingSettingsEndpointsTests()
    {
        File.WriteAllText(PathOf("appsettings.json"), AppSettings);
        File.WriteAllText(PathOf("appsettings.Development.json"), """{ "Serilog": { "MinimumLevel": { "Override": { "Behind": "Debug" } } } }""");
    }

    public void Dispose() => Directory.Delete(_directory, true);

    private string PathOf(string name) => Path.Combine(_directory, name);

    private ConfigurationFileCatalog Catalog => ConfigurationFileCatalog.For(_directory);

    private static IOptions<SecurityConfiguration> Security(bool readOnly = false) =>
        Options.Create(new SecurityConfiguration(readOnly, None, None, None));

    private JsonNode Levels(string file) =>
        JsonNode.Parse(File.ReadAllText(PathOf(file)))!["Serilog"]!["MinimumLevel"]!;

    private static readonly OpenTelemetrySettingsRequest DisabledOpenTelemetry = new(
        string.Empty,
        false,
        new OpenTelemetryExporterSettingsRequest(false, null, null, []),
        new OpenTelemetryExporterSettingsRequest(false, null, null, []),
        new OpenTelemetryLoggingSettingsRequest(false, null, null, [], null, null, null));

    private Task<IResult> Update(string file, LoggingSettingsRequest logging, bool readOnly = false) =>
        SettingsEndpoints.Update(
            file,
            new SettingsUpdateRequest(new Dictionary<string, JsonElement>(), logging, DisabledOpenTelemetry),
            Catalog,
            TestLocales.Embedded,
            Security(readOnly),
            _logger,
            CancellationToken.None);

    [Fact]
    public async Task Get_ListsTheAppSettingsFileWithTheLoggingEditorAndAllowedHosts()
    {
        var response = Assert.IsType<JsonHttpResult<SettingsResponse>>(
            await SettingsEndpoints.Get(Catalog, TestLocales.Embedded, Security(), CancellationToken.None)).Value!;

        var entry = response.Files.Single();
        Assert.Equal("appsettings.json", entry.Name);
        Assert.Equal("logging", entry.Editor);
        Assert.Equal("*", Assert.Single(entry.Fields, field => field.Path == "AllowedHosts").Value);
    }

    [Fact]
    public async Task Update_SavesAllowedHostsAlongsideLogging()
    {
        var request = new SettingsUpdateRequest(
            new Dictionary<string, JsonElement> { ["AllowedHosts"] = JsonDocument.Parse("\"example.org\"").RootElement.Clone() },
            new LoggingSettingsRequest("Warning", []), DisabledOpenTelemetry);
        var result = await SettingsEndpoints.Update("appsettings.json", request, Catalog, TestLocales.Embedded, Security(), _logger, CancellationToken.None);
        Assert.IsType<NoContent>(result);
        Assert.Equal("example.org", (string?)JsonNode.Parse(File.ReadAllText(PathOf("appsettings.json")))!["AllowedHosts"]);
        Assert.Equal("Warning", (string?)Levels("appsettings.json")["Default"]);
    }

    [Fact]
    public async Task Get_OffersSerilogCollectionsAndMasksNestedSecrets()
    {
        File.WriteAllText(PathOf("appsettings.json"),
            """{"Serilog":{"WriteTo":[{"Name":"Custom","Args":{"apiKey":"my-secret-value","other":{"Password":"another-secret"}}}]}}""");

        var response = Assert.IsType<JsonHttpResult<SettingsResponse>>(
            await SettingsEndpoints.Get(Catalog, TestLocales.Embedded, Security(), CancellationToken.None)).Value!;
        var fields = Assert.Single(response.Files).Fields;
        Assert.All(new[] { "Serilog:Using", "Serilog:Enrich", "Serilog:WriteTo", "Serilog:AuditTo", "Serilog:Filter", "Serilog:Destructure" }, path =>
            Assert.Equal("array", Assert.Single(fields, field => field.Path == path).Type));
        Assert.DoesNotContain(fields, field => field.Path.StartsWith("Serilog:WriteTo:", StringComparison.OrdinalIgnoreCase));
        var serialized = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("my-secret-value", serialized);
        Assert.DoesNotContain("another-secret", serialized);
        Assert.Contains("my-secret-value".MaskSensitiveValue(), serialized);
    }

    [Fact]
    public async Task Get_UsesEmbeddedDefaultsForMissingSerilogCollectionsWithoutWriting()
    {
        var before = File.ReadAllText(PathOf("appsettings.json"));
        var response = Assert.IsType<JsonHttpResult<SettingsResponse>>(
            await SettingsEndpoints.Get(Catalog, TestLocales.Embedded, Security(), CancellationToken.None)).Value!;
        var fields = Assert.Single(response.Files).Fields;

        Assert.Contains("Serilog.Sinks.Console", Assert.Single(fields, field => field.Path == "Serilog:Using").Value);
        Assert.Contains("FromLogContext", Assert.Single(fields, field => field.Path == "Serilog:Enrich").Value);
        Assert.Contains("Console", Assert.Single(fields, field => field.Path == "Serilog:WriteTo").Value);
        Assert.Equal("[]", Assert.Single(fields, field => field.Path == "Serilog:AuditTo").Value);
        Assert.Equal(before, File.ReadAllText(PathOf("appsettings.json")));
    }

    [Fact]
    public async Task Get_NonArraySerilogCollectionKeepsItsExistingScalarFields()
    {
        File.WriteAllText(PathOf("appsettings.json"), """{"Serilog":{"WriteTo":{"Console":{"Name":"Console"}}}}""");
        var response = Assert.IsType<JsonHttpResult<SettingsResponse>>(
            await SettingsEndpoints.Get(Catalog, TestLocales.Embedded, Security(), CancellationToken.None)).Value!;
        var fields = Assert.Single(response.Files).Fields;
        Assert.DoesNotContain(fields, field => field.Path == "Serilog:WriteTo");
        Assert.Equal("Console", Assert.Single(fields, field => field.Path == "Serilog:WriteTo:Console:Name").Value);
    }

    [Fact]
    public async Task Update_SerilogCollectionsPreservesNestedSecretsWhenEntriesMove()
    {
        File.WriteAllText(PathOf("appsettings.json"),
            """{"Serilog":{"WriteTo":[{"Name":"Remove"},{"Name":"Custom","Args":{"apiKey":"my-secret-value","other":{"Password":"another-secret"},"token":12345678,"custom":{"value":7}}}]}}""");
        var response = Assert.IsType<JsonHttpResult<SettingsResponse>>(
            await SettingsEndpoints.Get(Catalog, TestLocales.Embedded, Security(), CancellationToken.None)).Value!;
        var field = Assert.Single(Assert.Single(response.Files).Fields, field => field.Path == "Serilog:WriteTo");
        Assert.DoesNotContain("12345678", field.Value);
        var entries = JsonNode.Parse(field.Value)!.AsArray();
        entries.RemoveAt(0);
        entries[0]!["Name"] = "Changed";
        var request = new SettingsUpdateRequest(
            new Dictionary<string, JsonElement> { [field.Path] = JsonSerializer.SerializeToElement(entries) },
            new LoggingSettingsRequest("Warning", []), DisabledOpenTelemetry);
        Assert.True(JsonSchemas.IsValid("settings-response-1.1.0",
            JsonSerializer.SerializeToNode(response, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        Assert.True(JsonSchemas.IsValid("settings-update-1.1.0",
            JsonSerializer.SerializeToNode(request, new JsonSerializerOptions(JsonSerializerDefaults.Web))));

        Assert.IsType<NoContent>(await SettingsEndpoints.Update(
            "appsettings.json", request, Catalog, TestLocales.Embedded, Security(), _logger, CancellationToken.None));

        var saved = JsonNode.Parse(File.ReadAllText(PathOf("appsettings.json")))!["Serilog"]!["WriteTo"]![0]!;
        Assert.Equal("Changed", (string?)saved["Name"]);
        Assert.Equal("my-secret-value", (string?)saved["Args"]!["apiKey"]);
        Assert.Equal("another-secret", (string?)saved["Args"]!["other"]!["Password"]);
        Assert.Equal(12345678, (int?)saved["Args"]!["token"]);
        Assert.Equal(7, (int?)saved["Args"]!["custom"]!["value"]);
    }

    [Fact]
    public async Task Update_AmbiguousMaskedSerilogSecretsIsRejectedWithoutWriting()
    {
        File.WriteAllText(PathOf("appsettings.json"),
            """{"Serilog":{"WriteTo":[{"Name":"A","Args":{"apiKey":"ab1234yz"}},{"Name":"B","Args":{"apiKey":"ab5678yz"}}]}}""");
        var before = File.ReadAllText(PathOf("appsettings.json"));
        var request = new SettingsUpdateRequest(
            new Dictionary<string, JsonElement>
            {
                ["Serilog:WriteTo"] = JsonDocument.Parse("""[{"Name":"B","Args":{"apiKey":"ab....yz"}}]""").RootElement.Clone()
            }, new LoggingSettingsRequest("Warning", []), DisabledOpenTelemetry);

        Assert.IsType<BadRequest>(await SettingsEndpoints.Update(
            "appsettings.json", request, Catalog, TestLocales.Embedded, Security(), _logger, CancellationToken.None));
        Assert.Equal(before, File.ReadAllText(PathOf("appsettings.json")));
    }

    [Fact]
    public void SerilogBlankSecretsPreserveOnlyTheSameUnambiguousEntry()
    {
        var existing = JsonNode.Parse(
            """[{"Name":"First","Args":{"Password":"first-secret"}},{"Name":"Second","Args":{"Password":"second-secret"}}]""")!;
        var unchanged = JsonDocument.Parse(
            """[{"Name":"First","Args":{"Password":""}},{"Name":"Second","Args":{"Password":"  "}}]""").RootElement;
        var result = SerilogSettings.Merge(existing, unchanged).GetOrElse(new JsonArray());
        Assert.Equal("first-secret", (string?)result[0]!["Args"]!["Password"]);
        Assert.Equal("second-secret", (string?)result[1]!["Args"]!["Password"]);

        var reordered = JsonDocument.Parse(
            """[{"Name":"Second","Args":{"Password":""}},{"Name":"First","Args":{"Password":""}}]""").RootElement;
        Assert.Equal(None, SerilogSettings.Merge(existing, reordered));
        var duplicate = JsonNode.Parse(
            """[{"Name":"First","Args":{"Password":"one"}},{"Name":"First","Args":{"Password":"two"}}]""")!;
        Assert.Equal(None, SerilogSettings.Merge(duplicate, JsonDocument.Parse(
            """[{"Name":"First","Args":{"Password":""}},{"Name":"First","Args":{"Password":""}}]""").RootElement));
    }

    [Fact]
    public async Task Update_DuplicateSerilogArgumentNamesAreRejectedWithoutWriting()
    {
        var before = File.ReadAllText(PathOf("appsettings.json"));
        var request = new SettingsUpdateRequest(
            new Dictionary<string, JsonElement>
            {
                ["Serilog:WriteTo"] = JsonDocument.Parse("""[{"Name":"Custom","Args":{"Token":"a","token":"b"}}]""").RootElement.Clone()
            }, new LoggingSettingsRequest("Warning", []), DisabledOpenTelemetry);

        Assert.IsType<BadRequest>(await SettingsEndpoints.Update(
            "appsettings.json", request, Catalog, TestLocales.Embedded, Security(), _logger, CancellationToken.None));
        Assert.Equal(before, File.ReadAllText(PathOf("appsettings.json")));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("{}")]
    [InlineData("{\"Name\":123}")]
    [InlineData("{\"Name\":\"  \"}")]
    public async Task Update_InvalidSerilogCollectionEntryIsRejectedWithoutWriting(string entry)
    {
        var before = File.ReadAllText(PathOf("appsettings.json"));
        var request = new SettingsUpdateRequest(
            new Dictionary<string, JsonElement>
            {
                ["Serilog:WriteTo"] = JsonDocument.Parse($"[{entry}]").RootElement.Clone()
            }, new LoggingSettingsRequest("Warning", []), DisabledOpenTelemetry);

        Assert.IsType<BadRequest>(await SettingsEndpoints.Update(
            "appsettings.json", request, Catalog, TestLocales.Embedded, Security(), _logger, CancellationToken.None));
        Assert.Equal(before, File.ReadAllText(PathOf("appsettings.json")));
    }

    [Fact]
    public async Task Update_SerilogUsingRejectsObjectEntries()
    {
        var before = File.ReadAllText(PathOf("appsettings.json"));
        var request = new SettingsUpdateRequest(
            new Dictionary<string, JsonElement>
            {
                ["Serilog:Using"] = JsonDocument.Parse("""[{"Name":"Serilog.Sinks.Console"}]""").RootElement.Clone()
            }, new LoggingSettingsRequest("Warning", []), DisabledOpenTelemetry);

        Assert.IsType<BadRequest>(await SettingsEndpoints.Update(
            "appsettings.json", request, Catalog, TestLocales.Embedded, Security(), _logger, CancellationToken.None));
        Assert.Equal(before, File.ReadAllText(PathOf("appsettings.json")));
    }

    [Fact]
    public async Task Update_SerilogDirectiveNameIsCaseInsensitive()
    {
        var before = File.ReadAllText(PathOf("appsettings.json"));
        var request = new SettingsUpdateRequest(
            new Dictionary<string, JsonElement>
            {
                ["Serilog:WriteTo"] = JsonDocument.Parse("""[{"name":"Console"}]""").RootElement.Clone()
            }, new LoggingSettingsRequest("Warning", []), DisabledOpenTelemetry);

        Assert.IsType<NoContent>(await SettingsEndpoints.Update(
            "appsettings.json", request, Catalog, TestLocales.Embedded, Security(), _logger, CancellationToken.None));
        Assert.NotEqual(before, File.ReadAllText(PathOf("appsettings.json")));
    }

    [Theory]
    [InlineData("Serilog:Enrich", """["FromLogContext"]""")]
    [InlineData("Serilog:WriteTo", """["Console"]""")]
    public async Task Update_AcceptsStringShorthandInDirectiveCollections(string path, string json)
    {
        var request = new SettingsUpdateRequest(
            new Dictionary<string, JsonElement>
            {
                [path] = JsonDocument.Parse(json).RootElement.Clone()
            }, new LoggingSettingsRequest("Warning", []), DisabledOpenTelemetry);

        Assert.IsType<NoContent>(await SettingsEndpoints.Update(
            "appsettings.json", request, Catalog, TestLocales.Embedded, Security(), _logger, CancellationToken.None));
        Assert.Equal(json, JsonNode.Parse(File.ReadAllText(PathOf("appsettings.json")))!
            ["Serilog"]![path.Split(':')[1]]!.ToJsonString());
    }

    [Fact]
    public async Task GetLogging_ReadsTheLevels()
    {
        var result = await SettingsEndpoints.GetLogging("appsettings.json", Catalog, CancellationToken.None);

        var settings = Assert.IsType<JsonHttpResult<LoggingSettingsResponse>>(result).Value!;
        Assert.Equal("Information", settings.Default);
        Assert.Equal([new LoggingOverrideSettings("Microsoft.AspNetCore", "Warning")], settings.Overrides);
        Assert.Contains("Debug", settings.Levels);
    }

    [Theory]
    [InlineData("appsettings.Development.json")]
    [InlineData("backend.json")]
    [InlineData("email.json")]
    [InlineData("../appsettings.json")]
    [InlineData("missing.json")]
    public async Task GetLogging_OtherFiles_AreNotFound(string file)
    {
        Assert.IsType<NotFound>(await SettingsEndpoints.GetLogging(file, Catalog, CancellationToken.None));
    }

    [Fact]
    public async Task Update_WritesTheChosenLevelsAndKeepsTheRestOfTheFile()
    {
        var result = await Update(
            "appsettings.json",
            new LoggingSettingsRequest("Warning", [new LoggingOverrideSettings("Behind", "Debug")]));

        Assert.IsType<NoContent>(result);
        Assert.Equal("""{"Default":"Warning","Override":{"Behind":"Debug"}}""", Levels("appsettings.json").ToJsonString());
        Assert.Equal("*", (string)JsonNode.Parse(File.ReadAllText(PathOf("appsettings.json")))!["AllowedHosts"]!);
    }

    [Fact]
    public async Task Update_InvalidLevel_IsRejectedAndWritesNothing()
    {
        var before = File.ReadAllText(PathOf("appsettings.json"));

        var result = await Update("appsettings.json", new LoggingSettingsRequest("Loud", []));

        Assert.IsType<BadRequest>(result);
        Assert.Equal(before, File.ReadAllText(PathOf("appsettings.json")));
    }

    [Fact]
    public async Task Update_OtherFile_IsNotFound()
    {
        var request = new SettingsUpdateRequest(new Dictionary<string, JsonElement>());

        var result = await SettingsEndpoints.Update(
            "backend.json",
            request,
            Catalog,
            TestLocales.Embedded,
            Security(),
            _logger,
            CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Update_ReadOnlyConfiguration_IsForbidden()
    {
        var before = File.ReadAllText(PathOf("appsettings.json"));

        var result = await Update("appsettings.json", new LoggingSettingsRequest("Debug", []), readOnly: true);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<JsonHttpResult<ConfigurationForbiddenResponse>>(result).StatusCode);
        Assert.Equal(before, File.ReadAllText(PathOf("appsettings.json")));
    }

    [Fact]
    public async Task GenericUpdate_RefusesTheAppSettingsFiles()
    {
        var request = new SettingsUpdateRequest(
            new Dictionary<string, JsonElement>
            {
                ["AllowedHosts"] = JsonDocument.Parse("\"x\"").RootElement.Clone()
            });

        var result = await SettingsEndpoints.Update("appsettings.json", request, Catalog, TestLocales.Embedded, Security(), _logger, CancellationToken.None);

        Assert.IsType<BadRequest>(result);
    }
}
