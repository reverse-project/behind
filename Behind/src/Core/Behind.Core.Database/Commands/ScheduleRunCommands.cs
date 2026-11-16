using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Commands;

public sealed class ScheduleRunCommands(AbstractCoreContext context)
    : VersionedCommands<ScheduleRun, IScheduleRunCommands.ScheduleRunDraft>(context), IScheduleRunCommands
{
    public const string InvalidReason = "invalid";

    protected override ScheduleRun Build(IScheduleRunCommands.ScheduleRunDraft draft) =>
        new(
            new VersionedKey<Guid>(Guid.Empty),
            draft.ScheduleId,
            Truncate(draft.ScheduleName, Schedule.MaxNameLength),
            Truncate(draft.Action, Schedule.MaxActionLength),
            draft.ScheduledFor,
            draft.StartedAt,
            draft.FinishedAt,
            draft.Status,
            Truncate(draft.Summary, ScheduleRun.MaxSummaryLength),
            draft.Link,
            draft.Email,
            draft.Recipients);

    protected override Task<Option<string>> Validate(
        Guid key,
        IScheduleRunCommands.ScheduleRunDraft draft,
        CancellationToken cancellationToken) =>
        Task.FromResult<Option<string>>(
            draft.ScheduleId == Guid.Empty
            || draft.Recipients < 0
            || draft.Link is { Length: > ScheduleRun.MaxLinkLength }
            || !Enum.IsDefined(draft.Status)
            || !Enum.IsDefined(draft.Email)
                ? Some(InvalidReason)
                : None);

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];
}
