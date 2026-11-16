using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Behind.Tests.Helpers;

public sealed record CapturedLog(LogLevel Level, string Message, Exception? Exception, IReadOnlyDictionary<string, object?> State);

public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();

    public IReadOnlyList<CapturedLog> Entries => [.. _entries];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value)
            : new Dictionary<string, object?>();

        _entries.Enqueue(new CapturedLog(logLevel, formatter(state, exception), exception, values));
    }
}

public static class CapturingLoggerExtensions
{
    extension<T>(CapturingLogger<T> logger)
    {
        public CapturedLog Single(LogLevel level) => logger.Entries.Single(entry => entry.Level == level);

        public bool Has(LogLevel level) => logger.Entries.Any(entry => entry.Level == level);

        public async Task WaitForAsync(LogLevel level)
        {
            while (!logger.Has(level))
            {
                await Task.Delay(10);
            }
        }
    }
}
