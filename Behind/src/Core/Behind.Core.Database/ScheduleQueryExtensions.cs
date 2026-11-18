using Behind.Common.Database;
using Behind.Common.Scheduling;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

internal sealed record ScheduleRow(
    Guid Key,
    long Version,
    string Name,
    CronExpression Cron,
    string Action,
    ScheduleParameters Parameters,
    bool Enabled,
    ScheduleNotification Notify,
    ScheduleParticipants Participants);

internal sealed record ScheduleRunRow(
    Guid Key,
    long Version,
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

internal static class ScheduleQueryExtensions
{
    extension(IQueryable<Schedule> schedules)
    {
        public IQueryable<Schedule> Ordered() =>
            schedules
                .OrderBy(schedule => schedule.Name)
                .ThenBy(schedule => EF.Property<Guid>(schedule, VersionedColumns.Key));

        public IQueryable<ScheduleRow> ToRows() =>
            schedules.Select(schedule => new ScheduleRow(
                EF.Property<Guid>(schedule, VersionedColumns.Key),
                EF.Property<long>(schedule, VersionedColumns.Version),
                schedule.Name,
                schedule.Cron,
                schedule.Action,
                schedule.Parameters,
                schedule.Enabled,
                schedule.Notify,
                schedule.Participants));
    }

    extension(IQueryable<ScheduleRun> runs)
    {
        public IQueryable<ScheduleRun> Ordered() =>
            runs
                .OrderByDescending(run => run.StartedAt)
                .ThenByDescending(run => EF.Property<Guid>(run, VersionedColumns.Key));

        public IQueryable<ScheduleRunRow> ToRows() =>
            runs.Select(run => new ScheduleRunRow(
                EF.Property<Guid>(run, VersionedColumns.Key),
                EF.Property<long>(run, VersionedColumns.Version),
                run.ScheduleId,
                run.ScheduleName,
                run.Action,
                run.ScheduledFor,
                run.StartedAt,
                run.FinishedAt,
                run.Status,
                run.Summary,
                run.Link,
                run.Email,
                run.Recipients));
    }

    extension(AbstractCoreContext context)
    {
        public IQueryable<Schedule> LatestSchedulesIn(DeletionState state) =>
            context.Schedules
                .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
                .LatestOf(context.Schedules);

        public IQueryable<ScheduleRun> LatestScheduleRunsIn(DeletionState state) =>
            context.ScheduleRuns
                .InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
                .LatestOf(context.ScheduleRuns);
    }
}
