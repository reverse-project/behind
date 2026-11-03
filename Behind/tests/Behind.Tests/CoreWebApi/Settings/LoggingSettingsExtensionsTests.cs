using System.Text.Json.Nodes;
using Behind.Common.Logging;
using Behind.Core.WebApi.Settings;
using Serilog.Events;

namespace Behind.Tests.CoreWebApi.Settings;

public sealed class LoggingSettingsExtensionsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, true);

    private FileInfo FileWith(string content)
    {
        var path = Path.Combine(_directory, "appsettings.json");
        File.WriteAllText(path, content);
        return new FileInfo(path);
    }

    private static LoggingOverrideSettings Override(string source, string level) => new(source, level);

    private static MinimumLevelConfiguration Configured(LoggingSettingsRequest request) =>
        request.ToConfiguration().Match(() => throw new InvalidOperationException("The request was refused."), configuration => configuration);

    [Fact]
    public async Task ReadMinimumLevelAsync_ReadsTheSerilogSection()
    {
        var file = FileWith("""{ "Serilog": { "MinimumLevel": { "Default": "Information", "Override": { "Microsoft": "Warning" } } } }""");

        var configuration = await file.ReadMinimumLevelAsync();

        Assert.Equal(Some(LogEventLevel.Information), configuration.Default);
        Assert.Equal(LogEventLevel.Warning, configuration.Overrides["Microsoft"]);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "Serilog": {} }""")]
    [InlineData("""{ "Serilog": { "MinimumLevel": null } }""")]
    public async Task ReadMinimumLevelAsync_WithoutLevels_IsEmpty(string json)
    {
        Assert.True((await FileWith(json).ReadMinimumLevelAsync()).IsEmpty);
    }

    [Fact]
    public async Task WriteMinimumLevelAsync_ReplacesOnlyTheLevelsAndKeepsTheRestOfTheFile()
    {
        var file = FileWith("""
                            { "AllowedHosts": "*", "Serilog": { "Using": ["Serilog.Sinks.Console"], "MinimumLevel": { "Default": "Information", "Override": { "Old": "Debug" } } } }
                            """);
        var configuration = new MinimumLevelConfiguration(
            Some(LogEventLevel.Warning),
            new Dictionary<string, LogEventLevel> { ["Behind"] = LogEventLevel.Debug });

        await file.WriteMinimumLevelAsync(configuration);

        var root = JsonNode.Parse(File.ReadAllText(file.FullName))!;
        Assert.Equal("*", (string)root["AllowedHosts"]!);
        Assert.Equal("Serilog.Sinks.Console", (string)root["Serilog"]!["Using"]![0]!);
        Assert.Equal("""{"Default":"Warning","Override":{"Behind":"Debug"}}""", root["Serilog"]!["MinimumLevel"]!.ToJsonString());
    }

    [Fact]
    public async Task WriteMinimumLevelAsync_EmptyConfiguration_RemovesTheSection()
    {
        var file = FileWith("""{ "Serilog": { "MinimumLevel": { "Default": "Debug" }, "Enrich": ["x"] } }""");

        await file.WriteMinimumLevelAsync(MinimumLevelConfiguration.Empty);

        var serilog = JsonNode.Parse(File.ReadAllText(file.FullName))!["Serilog"]!.AsObject();
        Assert.False(serilog.ContainsKey("MinimumLevel"));
        Assert.True(serilog.ContainsKey("Enrich"));
    }

    [Fact]
    public async Task WriteMinimumLevelAsync_CreatesMissingSections()
    {
        var file = FileWith("{}");

        await file.WriteMinimumLevelAsync(new MinimumLevelConfiguration(Some(LogEventLevel.Error), new Dictionary<string, LogEventLevel>()));

        Assert.Equal("Error", (string)JsonNode.Parse(File.ReadAllText(file.FullName))!["Serilog"]!["MinimumLevel"]!["Default"]!);
    }

    [Fact]
    public void ToSettings_ListsTheLevelsAndSortsTheOverrides()
    {
        var settings = new MinimumLevelConfiguration(
            Some(LogEventLevel.Information),
            new Dictionary<string, LogEventLevel> { ["microsoft"] = LogEventLevel.Warning, ["Behind"] = LogEventLevel.Debug })
            .ToSettings();

        Assert.Equal("Information", settings.Default);
        Assert.Equal(["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"], settings.Levels);
        Assert.Equal([Override("Behind", "Debug"), Override("microsoft", "Warning")], settings.Overrides);
    }

    [Fact]
    public void ToSettings_WithoutADefault_HasNoDefault()
    {
        Assert.Null(MinimumLevelConfiguration.Empty.ToSettings().Default);
    }

    [Fact]
    public void ToConfiguration_BuildsTheRecordAndTrimsTheSources()
    {
        var configuration = Configured(new LoggingSettingsRequest("warning", [Override(" Behind ", "debug"), Override("Microsoft", "Error")]));

        Assert.Equal(Some(LogEventLevel.Warning), configuration.Default);
        Assert.Equal(LogEventLevel.Debug, configuration.Overrides["Behind"]);
        Assert.Equal(LogEventLevel.Error, configuration.Overrides["Microsoft"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ToConfiguration_BlankDefault_MeansNoDefault(string? value)
    {
        Assert.Equal(None, Configured(new LoggingSettingsRequest(value, [])).Default);
    }

    public static TheoryData<LoggingSettingsRequest> InvalidRequests =>
        new()
        {
            new LoggingSettingsRequest("Loud", []),
            new LoggingSettingsRequest("3", []),
            new LoggingSettingsRequest(null, [Override("A", "Loud")]),
            new LoggingSettingsRequest(null, [Override(" ", "Debug")]),
            new LoggingSettingsRequest(null, [Override("A", "Debug"), Override("a", "Error")])
        };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void ToConfiguration_InvalidRequests_AreRefused(LoggingSettingsRequest request)
    {
        Assert.Equal(None, request.ToConfiguration());
    }
}
