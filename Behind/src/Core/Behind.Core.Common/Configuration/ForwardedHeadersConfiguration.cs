namespace Behind.Core.Common.Configuration;

public sealed record ForwardedHeadersConfiguration
{
    public const string SectionName = "Backend:ForwardedHeaders";

    public bool Enabled { get; init; }

    public string[] KnownForwarders { get; init; } = [];

    public int ForwardLimit { get; init; } = 1;
}
