using Behind.Common;
using Behind.Core.Database;

namespace Behind.Core.WebApi.Scheduling;

public interface IScheduleRunner
{
    bool IsRunning(Guid scheduleId);

    Task<Option<Guid>> TryStart(
        IScheduleQueries.ScheduleDetails schedule,
        DateTimeOffset scheduledFor,
        CancellationToken cancellationToken = default);

    Task WhenIdle();

    Task<int> RecoverInterrupted(CancellationToken cancellationToken = default);

    Task Stop();
}
