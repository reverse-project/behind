namespace Behind.Core.Common.Configuration;

public sealed record CarouselsConfiguration
{
    public const string SectionName = $"{FrontendConfiguration.SectionName}:Carousels";
    public const int DefaultMaxSlides = 20;
    public const int DefaultMinIntervalSeconds = 2;
    public const int DefaultMaxIntervalSeconds = 60;
    public const int DefaultIntervalSeconds = 5;
    public const int DefaultMaxCaptionLength = 300;
    public const int CaptionCeiling = 1_000;
    public const long DefaultMaxImageBytes = 5L * 1024 * 1024;

    public int MaxSlides { get; init; } = DefaultMaxSlides;

    public int MinIntervalSeconds { get; init; } = DefaultMinIntervalSeconds;

    public int MaxIntervalSeconds { get; init; } = DefaultMaxIntervalSeconds;

    public int IntervalSeconds { get; init; } = DefaultIntervalSeconds;

    public int MaxCaptionLength { get; init; } = DefaultMaxCaptionLength;

    public long MaxImageBytes { get; init; } = DefaultMaxImageBytes;

    public int SlideLimit => MaxSlides > 0 ? MaxSlides : DefaultMaxSlides;

    public int CaptionLimit =>
        MaxCaptionLength > 0 ? Math.Min(MaxCaptionLength, CaptionCeiling) : DefaultMaxCaptionLength;

    public long ImageLimit => MaxImageBytes > 0 ? MaxImageBytes : DefaultMaxImageBytes;

    public (int Min, int Max) IntervalRange =>
        MinIntervalSeconds > 0 && MaxIntervalSeconds >= MinIntervalSeconds
            ? (MinIntervalSeconds, MaxIntervalSeconds)
            : (DefaultMinIntervalSeconds, DefaultMaxIntervalSeconds);

    public int DefaultInterval => Math.Clamp(IntervalSeconds, IntervalRange.Min, IntervalRange.Max);

    public bool IsValidInterval(int seconds) => seconds >= IntervalRange.Min && seconds <= IntervalRange.Max;
}
