using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Scheduling;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IScheduleQueries
{
    public sealed record ScheduleListing(
        VersionedKey<Guid> Id,
        string Name,
        CronExpression Cron,
        string Action,
        bool Enabled,
        ScheduleNotification Notify);

    public sealed record ScheduleDetails(
        VersionedKey<Guid> Id,
        string Name,
        CronExpression Cron,
        string Action,
        ScheduleParameters Parameters,
        bool Enabled,
        ScheduleNotification Notify,
        ScheduleParticipants Participants);

    Task<PagedResult<ScheduleListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<Option<ScheduleDetails>> Find(Guid key, DeletionState state, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduleDetails>> Enabled(CancellationToken cancellationToken = default);
}
