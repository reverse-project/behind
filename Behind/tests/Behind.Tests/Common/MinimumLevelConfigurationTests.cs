using System.Text.Json;
using Behind.Common.Logging;
using Serilog.Events;

namespace Behind.Tests.Common;

public class MinimumLevelConfigurationTests
{
    [Theory]
    [InlineData("Verbose", LogEventLevel.Verbose)]
    [InlineData("debug", LogEventLevel.Debug)]
    [InlineData("INFORMATION", LogEventLevel.Information)]
    [InlineData("Warning", LogEventLevel.Warning)]
    [InlineData("Error", LogEventLevel.Error)]
    [InlineData("Fatal", LogEventLevel.Fatal)]
    public void TryParseLevel_AcceptsTheLevelNames(string value, LogEventLevel expected)
    {
        Assert.True(MinimumLevelConfiguration.TryParseLevel(value, out var level));
        Assert.Equal(expected, level);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Loud")]
    [InlineData("3")]
    [InlineData("99")]
    public void TryParseLevel_RefusesEverythingElse(string? value)
    {
        Assert.False(MinimumLevelConfiguration.TryParseLevel(value, out _));
    }

    [Fact]
    public void Levels_AreAllTheSerilogLevels()
    {
        Assert.Equal(
            ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"],
            MinimumLevelConfiguration.Levels.Select(level => level.ToString()));
    }

    [Fact]
    public void Read_KeepsTheDefaultAndTheOverrides()
    {
        var configuration = JsonSerializer.Deserialize<MinimumLevelConfiguration>(
            """{ "Default": "Information", "Override": { "Microsoft": "Warning", "Behind": "Debug" } }""")!;

        Assert.Equal(Some(LogEventLevel.Information), configuration.Default);
        Assert.Equal(LogEventLevel.Warning, configuration.Overrides["Microsoft"]);
        Assert.Equal(LogEventLevel.Debug, configuration.Overrides["Behind"]);
    }

    [Fact]
    public void Read_IgnoresWhatIsNotALevel()
    {
        var configuration = JsonSerializer.Deserialize<MinimumLevelConfiguration>(
            """{ "Default": { "ControlledBy": "x" }, "Override": { "A": "Loud", "B": 3, "C": "Error" } }""")!;

        Assert.Equal(None, configuration.Default);
        Assert.Equal(["C"], configuration.Overrides.Keys);
    }

    [Fact]
    public void Read_NonObject_IsEmpty()
    {
        Assert.True(JsonSerializer.Deserialize<MinimumLevelConfiguration>("\"Information\"")!.IsEmpty);
    }

    [Fact]
    public void Write_ProducesTheSerilogShapeAndOmitsWhatIsEmpty()
    {
        var full = new MinimumLevelConfiguration(
            Some(LogEventLevel.Warning),
            new Dictionary<string, LogEventLevel> { ["Behind"] = LogEventLevel.Debug });
        var onlyDefault = new MinimumLevelConfiguration(Some(LogEventLevel.Error), new Dictionary<string, LogEventLevel>());

        Assert.Equal("""{"Default":"Warning","Override":{"Behind":"Debug"}}""", JsonSerializer.Serialize(full));
        Assert.Equal("""{"Default":"Error"}""", JsonSerializer.Serialize(onlyDefault));
        Assert.Equal("{}", JsonSerializer.Serialize(MinimumLevelConfiguration.Empty));
    }

    [Fact]
    public void IsEmpty_IsTrueOnlyWithoutADefaultAndOverrides()
    {
        Assert.True(MinimumLevelConfiguration.Empty.IsEmpty);
        Assert.False(new MinimumLevelConfiguration(Some(LogEventLevel.Debug), new Dictionary<string, LogEventLevel>()).IsEmpty);
        Assert.False(new MinimumLevelConfiguration(None, new Dictionary<string, LogEventLevel> { ["A"] = LogEventLevel.Debug }).IsEmpty);
    }
}
