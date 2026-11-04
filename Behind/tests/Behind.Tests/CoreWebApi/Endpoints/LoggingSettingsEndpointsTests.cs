using System.Text.Json.Nodes;
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

    private Task<IResult> Update(string file, LoggingSettingsRequest request, bool readOnly = false) =>
        SettingsEndpoints.UpdateLogging(file, request, Catalog, Security(readOnly), _logger, CancellationToken.None);

    [Fact]
    public async Task Get_ListsTheAppSettingsFileWithTheLoggingEditorAndNoRawFields()
    {
        var response = Assert.IsType<JsonHttpResult<SettingsResponse>>(
            await SettingsEndpoints.Get(Catalog, TestLocales.Embedded, Security(), CancellationToken.None)).Value!;

        var entry = response.Files.Single();
        Assert.Equal("appsettings.json", entry.Name);
        Assert.Equal("logging", entry.Editor);
        Assert.Empty(entry.Fields);
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
        Assert.IsType<NotFound>(await Update("backend.json", new LoggingSettingsRequest("Debug", [])));
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
            new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["AllowedHosts"] = System.Text.Json.JsonDocument.Parse("\"x\"").RootElement.Clone()
            });

        var result = await SettingsEndpoints.Update("appsettings.json", request, Catalog, TestLocales.Embedded, Security(), _logger, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }
}
