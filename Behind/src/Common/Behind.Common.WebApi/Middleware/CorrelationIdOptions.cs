namespace Behind.Common.WebApi.Middleware;

public sealed record CorrelationIdOptions
{
    public const string DefaultHeaderName = "X-Correlation-ID";

    public static readonly CorrelationIdOptions Default = new();

    public string HeaderName { get; init; } = DefaultHeaderName;

    public int MaxLength { get; init; } = 128;

    public Func<string> Generator { get; init; } = static () => Guid.CreateVersion7().ToString();
}
