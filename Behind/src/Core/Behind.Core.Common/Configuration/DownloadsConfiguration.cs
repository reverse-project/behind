namespace Behind.Core.Common.Configuration;

public sealed record DownloadsConfiguration
{
    public const string SectionName = $"{BackendConfiguration.SectionName}:Downloads";
    public const long DefaultMaxFileBytes = 100L * 1024 * 1024;

    public long MaxFileBytes { get; init; } = DefaultMaxFileBytes;

    public long FileLimit => MaxFileBytes > 0 ? MaxFileBytes : DefaultMaxFileBytes;
}
