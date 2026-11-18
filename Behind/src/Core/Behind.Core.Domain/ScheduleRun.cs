using Behind.Common;

namespace Behind.Core.Domain;

public enum ScheduleRunStatus
{
    Running,
    Succeeded,
    Failed
}

public enum ScheduleRunEmail
{
    NotRequested,
    Sent,
    Failed,
    NoRecipients
}

public sealed record ScheduleRun(
    VersionedKey<Guid> Id,
    Guid ScheduleId,
    string ScheduleName,
    string Action,
    DateTimeOffset ScheduledFor,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    ScheduleRunStatus Status,
    string Summary,
    string? Link,
    ScheduleRunEmail Email,
    int Recipients)
{
    public const string EntityType = nameof(ScheduleRun);
    public const int MaxSummaryLength = 4_000;
    public const int MaxLinkLength = 2_000;
    public const int MaxEnumLength = 16;

    public ScheduleRun(
        Guid scheduleId,
        string scheduleName,
        string action,
        DateTimeOffset scheduledFor,
        DateTimeOffset startedAt)
        : this(
            new VersionedKey<Guid>(Guid.Empty),
            scheduleId,
            scheduleName,
            action,
            scheduledFor,
            startedAt,
            null,
            ScheduleRunStatus.Running,
            string.Empty,
            null,
            ScheduleRunEmail.NotRequested,
            0)
    {
    }
}
