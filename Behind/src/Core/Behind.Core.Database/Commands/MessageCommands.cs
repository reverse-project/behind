using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Commands;

public sealed class MessageCommands(AbstractCoreContext context)
    : VersionedCommands<Message, IMessageCommands.MessageDraft>(context), IMessageCommands
{
    public const string InvalidReason = "invalid";
    public const string InvalidScheduleReason = "invalid-schedule";

    protected override Message Build(IMessageCommands.MessageDraft draft) =>
        new(draft.Name.Trim(), draft.Title.Trim(), draft.Content, draft.Level, draft.StartsAt, draft.EndsAt);

    protected override Task<Option<string>> Validate(
        Guid key,
        IMessageCommands.MessageDraft draft,
        CancellationToken cancellationToken) =>
        Task.FromResult(Reason(Build(draft)));

    private static Option<string> Reason(Message message) =>
        message.Name.Length == 0
        || message.Name.Length > Message.MaxNameLength
        || message.Title.Length > Message.MaxTitleLength
        || message.Content.Length > Message.MaxContentLength
        || !Enum.IsDefined(message.Level)
            ? Some(InvalidReason)
            : message.HasValidSchedule
                ? None
                : Some(InvalidScheduleReason);
}
