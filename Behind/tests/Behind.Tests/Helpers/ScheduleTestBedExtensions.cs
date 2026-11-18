using Behind.Common.Scheduling;
using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;

namespace Behind.Tests.Helpers;

public static class ScheduleTestBedExtensions
{
    public const string TestAction = "test.action";

    public static CronExpression Cron(string text = "0 * * * *") =>
        CronExpression.TryParse(text).Match(() => throw new InvalidOperationException(text), cron => cron);

    public static IScheduleCommands.ScheduleDraft ScheduleDraft(
        string name = "Nightly audit",
        string cron = "0 * * * *",
        string action = TestAction,
        IReadOnlyDictionary<string, string>? parameters = null,
        bool enabled = true,
        ScheduleNotification notify = ScheduleNotification.Never,
        IReadOnlyList<ScheduleParticipant>? participants = null) =>
        new(
            name,
            Cron(cron),
            action,
            new ScheduleParameters(parameters ?? new Dictionary<string, string>()),
            enabled,
            notify,
            new ScheduleParticipants(participants ?? []));

    public static IScheduleRunCommands.ScheduleRunDraft ScheduleRunDraft(
        Guid scheduleId,
        DateTimeOffset startedAt,
        string scheduleName = "Nightly audit",
        ScheduleRunStatus status = ScheduleRunStatus.Succeeded,
        string summary = "Done",
        string? link = null) =>
        new(
            scheduleId,
            scheduleName,
            TestAction,
            startedAt,
            startedAt,
            status == ScheduleRunStatus.Running ? null : startedAt.AddMinutes(1),
            status,
            summary,
            link,
            ScheduleRunEmail.NotRequested,
            0);

    extension(NewsTestBed bed)
    {
        public async Task<TResult> WithScheduleCommandsAsync<TResult>(Func<ScheduleCommands, Task<TResult>> action)
        {
            await using var ctx = bed.Write();

            return await action(new ScheduleCommands(ctx));
        }

        public async Task<TResult> WithScheduleQueriesAsync<TResult>(Func<ScheduleQueries, Task<TResult>> action)
        {
            await using var ctx = bed.Read();

            return await action(new ScheduleQueries(ctx));
        }

        public async Task<TResult> WithScheduleRunCommandsAsync<TResult>(Func<ScheduleRunCommands, Task<TResult>> action)
        {
            await using var ctx = bed.Write();

            return await action(new ScheduleRunCommands(ctx));
        }

        public async Task<TResult> WithScheduleRunQueriesAsync<TResult>(Func<ScheduleRunQueries, Task<TResult>> action)
        {
            await using var ctx = bed.Read();

            return await action(new ScheduleRunQueries(ctx));
        }

        public async Task<Guid> CreateScheduleAsync(IScheduleCommands.ScheduleDraft? draft = null)
        {
            var result = await bed.WithScheduleCommandsAsync(commands => commands.Create(draft ?? ScheduleDraft()));

            return Assert.IsType<VersionedSaveResult.Saved>(result).Id.Key;
        }

        public async Task<Guid> CreateScheduleRunAsync(IScheduleRunCommands.ScheduleRunDraft draft)
        {
            var result = await bed.WithScheduleRunCommandsAsync(commands => commands.Create(draft));

            return Assert.IsType<VersionedSaveResult.Saved>(result).Id.Key;
        }
    }
}
