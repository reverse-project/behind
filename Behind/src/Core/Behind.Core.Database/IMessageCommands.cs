using Behind.Common.Database;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IMessageCommands : IVersionedCommands<IMessageCommands.MessageDraft>
{
    public sealed record MessageDraft(
        string Name,
        string Title,
        string Content,
        MessageLevel Level,
        DateTimeOffset? StartsAt,
        DateTimeOffset? EndsAt);
}
