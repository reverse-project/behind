using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class ScheduleRunQueries(AbstractCoreContext context) : IScheduleRunQueries
{
    public async Task<PagedResult<IScheduleRunQueries.ScheduleRunDetails>> List(
        Flipper flipper,
        Option<Guid> scheduleId,
        Option<ScheduleRunStatus> status,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var latest = context.LatestScheduleRunsIn(state);
        var bySchedule = scheduleId.Match(() => latest, id => latest.Where(run => run.ScheduleId == id));
        var matching = status.Match(() => bySchedule, value => bySchedule.Where(run => run.Status == value));
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(matching.Ordered().ToRows())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<IScheduleRunQueries.ScheduleRunDetails>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            rows.Count > 0
                ? Some<IEnumerable<IScheduleRunQueries.ScheduleRunDetails>>(rows.Select(ToDetails).ToList())
                : None);
    }

    public async Task<Option<IScheduleRunQueries.ScheduleRunDetails>> Find(
        Guid key,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return (await context.LatestScheduleRunsIn(state).OfKey(key).ToRows().Take(1)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Select(ToDetails)
            .Head();
    }

    public async Task<IReadOnlyList<IScheduleRunQueries.ScheduleRunDetails>> Running(
        CancellationToken cancellationToken = default) =>
        (await context.LatestScheduleRunsIn(DeletionState.Active)
            .Where(run => run.Status == ScheduleRunStatus.Running)
            .ToRows()
            .AsNoTracking()
            .ToListAsync(cancellationToken))
        .Select(ToDetails)
        .ToList();

    private static IScheduleRunQueries.ScheduleRunDetails ToDetails(ScheduleRunRow row) =>
        new(
            new VersionedKey<Guid>(row.Key, row.Version),
            row.ScheduleId,
            row.ScheduleName,
            row.Action,
            row.ScheduledFor,
            row.StartedAt,
            row.FinishedAt,
            row.Status,
            row.Summary,
            row.Link,
            row.Email,
            row.Recipients);

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;
}
