using Behind.Common.Logging;

namespace Behind.Tests.Common;

public class LoggingExtensionsTests
{
    private sealed class SwitchableLogger : ILogger
    {
        private readonly LogLevel _minimum;

        public SwitchableLogger(LogLevel minimum) => _minimum = minimum;

        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimum;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception, formatter(state, exception)));
    }

    [Fact]
    public void LogDebug_Disabled_DoesNotEvaluateArguments()
    {
        var logger = new SwitchableLogger(LogLevel.Information);
        var evaluated = false;

        logger.LogDebug("Value {Value}", () =>
        {
            evaluated = true;
            return [1];
        });

        Assert.False(evaluated);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void LogDebug_Enabled_FormatsLazilyEvaluatedArguments()
    {
        var logger = new SwitchableLogger(LogLevel.Debug);

        logger.LogDebug("Value {Value} and {Other}", () => [1, "two"]);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal("Value 1 and two", entry.Message);
    }

    [Theory]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    public void LevelShortcuts_LogAtTheirLevel(LogLevel level)
    {
        var logger = new SwitchableLogger(LogLevel.Debug);

        switch (level)
        {
            case LogLevel.Information:
                logger.LogInformation("Value {Value}", () => [1]);
                break;
            case LogLevel.Warning:
                logger.LogWarning("Value {Value}", () => [1]);
                break;
            default:
                logger.LogError("Value {Value}", () => [1]);
                break;
        }

        Assert.Equal(level, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public void ExceptionOverloads_PassTheExceptionAndSkipEvaluationWhenDisabled()
    {
        var failure = new InvalidOperationException("boom");
        var enabled = new SwitchableLogger(LogLevel.Debug);
        var disabled = new SwitchableLogger(LogLevel.Critical);
        var evaluated = false;

        enabled.LogError(failure, "Failed {Id}", () => [7]);
        enabled.LogWarning(failure, "Warned {Id}", () => [8]);
        disabled.LogError(failure, "Failed {Id}", () =>
        {
            evaluated = true;
            return [7];
        });

        Assert.Equal([LogLevel.Error, LogLevel.Warning], enabled.Entries.Select(e => e.Level));
        Assert.All(enabled.Entries, entry => Assert.Same(failure, entry.Exception));
        Assert.False(evaluated);
        Assert.Empty(disabled.Entries);
    }

    [Fact]
    public void Log_WithComputedLevel_UsesTheLevelForTheEnabledCheck()
    {
        var logger = new SwitchableLogger(LogLevel.Warning);
        var evaluations = 0;

        logger.Log(LogLevel.Information, "Value {Value}", () =>
        {
            evaluations++;
            return [1];
        });
        logger.Log(LogLevel.Warning, "Value {Value}", () =>
        {
            evaluations++;
            return [2];
        });

        Assert.Equal(1, evaluations);
        Assert.Equal("Value 2", Assert.Single(logger.Entries).Message);
    }
}
