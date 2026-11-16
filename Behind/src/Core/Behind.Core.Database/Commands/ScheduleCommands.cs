using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Commands;

public sealed class ScheduleCommands(AbstractCoreContext context)
    : VersionedCommands<Schedule, IScheduleCommands.ScheduleDraft>(context), IScheduleCommands
{
    public const string InvalidReason = "invalid";
    public const string InvalidParticipantsReason = "invalid-participants";

    protected override Schedule Build(IScheduleCommands.ScheduleDraft draft) =>
        new(
            draft.Name.Trim(),
            draft.Cron,
            draft.Action.Trim(),
            draft.Parameters,
            draft.Enabled,
            draft.Notify,
            draft.Participants.Normalized);

    protected override Task<Option<string>> Validate(
        Guid key,
        IScheduleCommands.ScheduleDraft draft,
        CancellationToken cancellationToken) =>
        Task.FromResult(Reason(Build(draft)));

    protected override async Task<bool> IsReferenced(Guid key, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();

        return await context.ScheduleRuns.AnyAsync(run => run.ScheduleId == key, cancellationToken);
    }

    private static Option<string> Reason(Schedule schedule) =>
        schedule.Name.Length == 0
        || schedule.Name.Length > Schedule.MaxNameLength
        || schedule.Cron.Text.Length > Schedule.MaxCronLength
        || schedule.Action.Length == 0
        || schedule.Action.Length > Schedule.MaxActionLength
        || schedule.Parameters.ToJson().Length > Schedule.MaxParametersLength
        || !Enum.IsDefined(schedule.Notify)
            ? Some(InvalidReason)
            : schedule.Participants.IsValid
              && schedule.Participants.ToJson().Length <= Schedule.MaxParticipantsLength
                ? None
                : Some(InvalidParticipantsReason);
}
