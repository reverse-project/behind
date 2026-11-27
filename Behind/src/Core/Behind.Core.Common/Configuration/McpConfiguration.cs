namespace Behind.Core.Common.Configuration;

public sealed record McpConfiguration
{
    public const string SectionName = $"{BackendConfiguration.SectionName}:Mcp";

    public bool Enabled { get; init; }

    public int MaxRequestBytes { get; init; } = 1048576;

    public int MaxResponseBytes { get; init; } = 1048576;

    public string[] AllowedOrigins { get; init; } = [];
}
