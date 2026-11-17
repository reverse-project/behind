using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IScheduleRunCommands : IVersionedCommands<IScheduleRunCommands.ScheduleRunDraft>
{
    public sealed record ScheduleRunDraft(
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
        int Recipients);
}
