using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IScheduleRunQueries
{
    public sealed record ScheduleRunDetails(
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
        int Recipients);

    Task<PagedResult<ScheduleRunDetails>> List(
        Flipper flipper,
        Option<Guid> scheduleId,
        Option<ScheduleRunStatus> status,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<Option<ScheduleRunDetails>> Find(Guid key, DeletionState state, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduleRunDetails>> Running(CancellationToken cancellationToken = default);
}
