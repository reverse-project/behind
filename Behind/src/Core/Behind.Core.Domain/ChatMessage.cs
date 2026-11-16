using Behind.Common;

namespace Behind.Core.Domain;

public sealed record ChatMessage(VersionedKey<Guid> Id, Guid AuthorId, string Text, Guid? ReplyToId, bool IsAnonymous = false)
{
    public const int MaxTextLength = 1000;
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(5);

    public ChatMessage(Guid authorId, string text, Guid? replyToId, bool isAnonymous = false)
        : this(new VersionedKey<Guid>(Guid.Empty), authorId, text, replyToId, isAnonymous)
    {
    }
}
