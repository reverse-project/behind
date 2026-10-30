using System.Diagnostics;

namespace Behind.Common.Logging;

public static class CorrelationContext
{
    public const string TelemetryAttribute = "correlation.id";

    private static readonly AsyncLocal<string?> Ambient = new();

    public static string ProcessId { get; } = Guid.CreateVersion7().ToString();

    public static string Current => Ambient.Value ?? ProcessId;

    public static IDisposable Begin(string correlationId)
    {
        var scope = new Scope(Ambient.Value);
        Ambient.Value = correlationId;
        Activity.Current?.SetTag(TelemetryAttribute, correlationId);
        return scope;
    }

    private sealed record Scope(string? Previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = Previous;
    }
}
