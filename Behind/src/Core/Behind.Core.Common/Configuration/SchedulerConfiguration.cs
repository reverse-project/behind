using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Common.Configuration;

public sealed record SchedulerConfiguration
{
    public const string SectionName = $"{BackendConfiguration.SectionName}:Scheduler";
    public const int DefaultTickSeconds = 60;
    public const int DefaultMaxConcurrentRuns = 4;
    public const int DefaultRunTimeoutMinutes = 120;
    public const int DefaultMaxCatchUpMinutes = 5;
    public const int DefaultPreviewCount = 5;
    public const int DefaultMaxPreviewCount = 50;
    public const int DefaultParticipantSearchLimit = 20;
    public const int DefaultSessionCleanupAgeDays = 30;
    public const int DefaultMaxSessionCleanupAgeDays = 3650;

    public bool Enabled { get; init; } = true;

    public int TickSeconds { get; init; } = DefaultTickSeconds;

    public int MaxConcurrentRuns { get; init; } = DefaultMaxConcurrentRuns;

    public int RunTimeoutMinutes { get; init; } = DefaultRunTimeoutMinutes;

    public int MaxCatchUpMinutes { get; init; } = DefaultMaxCatchUpMinutes;

    public int PreviewCount { get; init; } = DefaultPreviewCount;

    public int MaxPreviewCount { get; init; } = DefaultMaxPreviewCount;

    public int ParticipantSearchLimit { get; init; } = DefaultParticipantSearchLimit;

    public int MaxSessionCleanupAgeDays { get; init; } = DefaultMaxSessionCleanupAgeDays;

    public TimeSpan TickInterval => TimeSpan.FromSeconds(TickSeconds > 0 ? TickSeconds : DefaultTickSeconds);

    public int ConcurrencyLimit => MaxConcurrentRuns > 0 ? MaxConcurrentRuns : DefaultMaxConcurrentRuns;

    public Option<TimeSpan> RunTimeout =>
        RunTimeoutMinutes > 0 ? Some(TimeSpan.FromMinutes(RunTimeoutMinutes)) : None;

    public int CatchUpMinutes => MaxCatchUpMinutes >= 0 ? MaxCatchUpMinutes : DefaultMaxCatchUpMinutes;

    public int PreviewLimit => MaxPreviewCount > 0 ? MaxPreviewCount : DefaultMaxPreviewCount;

    public int DefaultPreview => Math.Clamp(PreviewCount > 0 ? PreviewCount : DefaultPreviewCount, 1, PreviewLimit);

    public int ParticipantLimit => ParticipantSearchLimit > 0 ? ParticipantSearchLimit : DefaultParticipantSearchLimit;

    public int SessionCleanupAgeLimit =>
        MaxSessionCleanupAgeDays > 0 ? MaxSessionCleanupAgeDays : DefaultMaxSessionCleanupAgeDays;
}
