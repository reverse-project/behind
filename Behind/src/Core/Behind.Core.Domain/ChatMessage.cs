using Behind.Common;

namespace Behind.Core.Domain;

public sealed record ChatMessage(VersionedKey<Guid> Id, Guid AuthorId, string Text, Guid? ReplyToId)
{
    public const int MaxTextLength = 1000;
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(5);

    public ChatMessage(Guid authorId, string text, Guid? replyToId)
        : this(new VersionedKey<Guid>(Guid.Empty), authorId, text, replyToId)
    {
    }
}
