namespace Behind.Core.Common.Configuration;

public sealed record SitesConfiguration
{
    public const string SectionName = $"{FrontendConfiguration.SectionName}:Sites";
    public const long DefaultMaxImageBytes = 5L * 1024 * 1024;

    public string? IndexSlug { get; init; }

    public long MaxImageBytes { get; init; } = DefaultMaxImageBytes;

    public long ImageLimit => MaxImageBytes > 0 ? MaxImageBytes : DefaultMaxImageBytes;
}
