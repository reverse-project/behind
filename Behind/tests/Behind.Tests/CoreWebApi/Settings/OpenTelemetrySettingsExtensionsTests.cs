using System.Text.Json.Nodes;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Settings;

public sealed class OpenTelemetrySettingsExtensionsTests : IDisposable
{
    private const string AppSettings = """
        { "AllowedHosts": "*", "OpenTelemetry": { "ServiceName": "test-service", "IsEnabled": true, "Traces": { "IsEnabled": true, "Endpoint": "https://otel.example/traces", "Protocol": "HttpProtobuf", "Instruments": ["AspNetCoreInstrumentation", "OtlpExporter"] }, "Metrics": { "IsEnabled": false, "Endpoint": null, "Protocol": null, "Instruments": ["RuntimeInstrumentation"] }, "Logging": { "IsEnabled": true, "Endpoint": "https://otel.example/logs", "Protocol": "Grpc", "Instruments": ["OtlpExporter"], "IncludeFormattedMessage": true, "IncludeScopes": false, "ParseStateValues": true } } }
        """;

    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;
    private readonly ILogger<SettingsEndpoints> _logger = NullLogger<SettingsEndpoints>.Instance;

    public OpenTelemetrySettingsExtensionsTests()
    {
        File.WriteAllText(PathOf("appsettings.json"), AppSettings);
        File.WriteAllText(PathOf("backend.json"), "{}");
    }

    public void Dispose() => Directory.Delete(_directory, true);

    private string PathOf(string name) => Path.Combine(_directory, name);

    private ConfigurationFileCatalog Catalog => ConfigurationFileCatalog.For(_directory);

    private static IOptions<SecurityConfiguration> Security(bool readOnly = false) =>
        Options.Create(new SecurityConfiguration(readOnly, None, None, None));

    private Task<IResult> Update(OpenTelemetrySettingsRequest request, bool readOnly = false) =>
        SettingsEndpoints.Update(
            "appsettings.json",
            new SettingsUpdateRequest(
                new Dictionary<string, System.Text.Json.JsonElement>(),
                new LoggingSettingsRequest(null, []),
                request),
            Catalog,
            TestLocales.Embedded,
            Security(readOnly),
            _logger,
            CancellationToken.None);

    private static OpenTelemetrySettingsRequest Request(
        string? endpoint = "https://otel.example/trace",
        string? protocol = "HttpProtobuf",
        IReadOnlyList<string>? instruments = null) =>
        new(
            "updated-service",
            true,
            new OpenTelemetryExporterSettingsRequest(true, endpoint, protocol, instruments ?? ["OtlpExporter"]),
            new OpenTelemetryExporterSettingsRequest(false, null, null, []),
            new OpenTelemetryLoggingSettingsRequest(
                true,
                "https://otel.example/logs",
                "Grpc",
                ["OtlpExporter"],
                true,
                true,
                false));

    [Fact]
    public async Task ReadOpenTelemetrySettingsAsync_ReadsAllExporterOptions()
    {
        var file = new FileInfo(PathOf("appsettings.json"));

        var settings = await file.ReadOpenTelemetrySettingsAsync();

        Assert.Equal("test-service", settings.ServiceName);
        Assert.True(settings.IsEnabled);
        Assert.Equal("https://otel.example/traces", settings.Traces.Endpoint);
        Assert.Equal("HttpProtobuf", settings.Traces.Protocol);
        Assert.Equal(["AspNetCoreInstrumentation", "OtlpExporter"], settings.Traces.Instruments);
        Assert.False(settings.Metrics.IsEnabled);
        Assert.Equal("Grpc", settings.Logging.Protocol);
        Assert.True(settings.Logging.IncludeFormattedMessage);
        Assert.False(settings.Logging.IncludeScopes);
        Assert.True(settings.Logging.ParseStateValues);
        Assert.Contains("HttpProtobuf", settings.Protocols);
    }

    [Fact]
    public async Task ReadOpenTelemetrySettingsAsync_MissingSectionReturnsDisabledDefaults()
    {
        File.WriteAllText(PathOf("appsettings.json"), """{ "AllowedHosts": "*" }""");

        var settings = await new FileInfo(PathOf("appsettings.json")).ReadOpenTelemetrySettingsAsync();

        Assert.False(settings.IsEnabled);
        Assert.Empty(settings.ServiceName);
        Assert.False(settings.Traces.IsEnabled);
        Assert.Empty(settings.Traces.Instruments);
        Assert.Null(settings.Logging.IncludeScopes);
    }

    [Fact]
    public async Task WriteOpenTelemetrySettingsAsync_ReplacesOnlyTheSectionAndKeepsStringNames()
    {
        var file = new FileInfo(PathOf("appsettings.json"));
        var request = Request(instruments: ["AspNetCoreInstrumentation", "OtlpExporter"]);
        var normalized = request.ToNormalizedRequest().Match(
            () => throw new InvalidOperationException("The request was refused."),
            configuration => configuration);

        await file.WriteOpenTelemetrySettingsAsync(normalized);

        var root = JsonNode.Parse(File.ReadAllText(file.FullName))!;
        Assert.Equal("*", (string)root["AllowedHosts"]!);
        Assert.Equal("updated-service", (string)root["OpenTelemetry"]!["ServiceName"]!);
        Assert.Equal("HttpProtobuf", (string)root["OpenTelemetry"]!["Traces"]!["Protocol"]!);
        Assert.Equal("OtlpExporter", (string)root["OpenTelemetry"]!["Traces"]!["Instruments"]![1]!);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void ToNormalizedRequest_InvalidConfigurationIsRejected(OpenTelemetrySettingsRequest request)
    {
        Assert.Equal(None, request.ToNormalizedRequest());
    }

    public static TheoryData<OpenTelemetrySettingsRequest> InvalidRequests =>
        new()
        {
            Request(endpoint: "not-an-absolute-uri"),
            Request(protocol: "UnknownProtocol"),
            Request(instruments: ["UnknownInstrumentation"]),
            Request(instruments: ["OtlpExporter", "OtlpExporter"]),
            Request(endpoint: null)
        };

    [Fact]
    public async Task GetOpenTelemetry_ReadsOnlyTheAppSettingsEditor()
    {
        var result = await SettingsEndpoints.GetOpenTelemetry("appsettings.json", Catalog, CancellationToken.None);

        var settings = Assert.IsType<JsonHttpResult<OpenTelemetrySettingsResponse>>(result).Value!;
        Assert.Equal("test-service", settings.ServiceName);
    }

    [Fact]
    public async Task GetOpenTelemetry_OtherFilesAreNotFound()
    {
        Assert.IsType<NotFound>(
            await SettingsEndpoints.GetOpenTelemetry("backend.json", Catalog, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAppSettings_WritesNormalizedOpenTelemetryValues()
    {
        var result = await Update(Request(instruments: ["OtlpExporter"]));

        Assert.IsType<NoContent>(result);
        var settings = await new FileInfo(PathOf("appsettings.json")).ReadOpenTelemetrySettingsAsync();
        Assert.Equal("updated-service", settings.ServiceName);
        Assert.Equal(["OtlpExporter"], settings.Traces.Instruments);
    }

    [Fact]
    public async Task UpdateAppSettings_InvalidOpenTelemetryConfigurationIsRejectedWithoutWriting()
    {
        var before = File.ReadAllText(PathOf("appsettings.json"));

        var result = await Update(Request(endpoint: null));

        Assert.IsType<BadRequest>(result);
        Assert.Equal(before, File.ReadAllText(PathOf("appsettings.json")));
    }

    [Fact]
    public async Task UpdateAppSettings_ReadOnlyConfigurationIsForbidden()
    {
        var before = File.ReadAllText(PathOf("appsettings.json"));

        var result = await Update(Request(), readOnly: true);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<JsonHttpResult<ConfigurationForbiddenResponse>>(result).StatusCode);
        Assert.Equal(before, File.ReadAllText(PathOf("appsettings.json")));
    }
}
