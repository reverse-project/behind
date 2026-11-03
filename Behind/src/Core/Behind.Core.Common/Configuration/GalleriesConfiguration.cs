namespace Behind.Core.Common.Configuration;

public sealed record GalleriesConfiguration
{
    public const string SectionName = $"{FrontendConfiguration.SectionName}:Galleries";
    public const long DefaultMaxImageBytes = 10L * 1024 * 1024;

    public long MaxImageBytes { get; init; } = DefaultMaxImageBytes;

    public long ImageLimit => MaxImageBytes > 0 ? MaxImageBytes : DefaultMaxImageBytes;
}
