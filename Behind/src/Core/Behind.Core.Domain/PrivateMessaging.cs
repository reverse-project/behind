using Behind.Common;

namespace Behind.Core.Domain;

public sealed record PrivateConversation(
    VersionedKey<Guid> Id,
    [property: Sensible] Guid SenderId,
    [property: Sensible] string Subject,
    bool IsGroup,
    bool IsSystem,
    bool IsReport)
{
    public PrivateConversation(Guid senderId, string subject, bool isGroup, bool isSystem, bool isReport)
        : this(new(Guid.Empty), senderId, subject, isGroup, isSystem, isReport) { }
}

public sealed record PrivateConversationRecipient(
    VersionedKey<Guid> Id,
    [property: Sensible] Guid ConversationId,
    [property: Sensible] Guid RecipientId,
    bool IsRole,
    bool IsHidden)
{
    public PrivateConversationRecipient(Guid conversationId, Guid recipientId, bool isRole, bool isHidden)
        : this(new(Guid.Empty), conversationId, recipientId, isRole, isHidden) { }
}

public sealed record PrivateMessage(
    VersionedKey<Guid> Id,
    [property: Sensible] Guid ConversationId,
    [property: Sensible] Guid AuthorId,
    [property: Sensible] string Content,
    [property: Sensible] Guid? ReplyToId,
    [property: Sensible] Guid? PrivateRecipientId,
    bool IsInitial)
{
    public PrivateMessage(Guid conversationId, Guid authorId, string content, Guid? replyToId, Guid? privateRecipientId, bool isInitial)
        : this(new(Guid.Empty), conversationId, authorId, content, replyToId, privateRecipientId, isInitial) { }
}

public sealed record PrivateMailbox(
    VersionedKey<Guid> Id,
    [property: Sensible] Guid ConversationId,
    [property: Sensible] Guid UserId,
    bool Deleted,
    bool Left)
{
    public PrivateMailbox(Guid conversationId, Guid userId, bool deleted, bool left)
        : this(new(Guid.Empty), conversationId, userId, deleted, left) { }
}

public sealed record PrivateMessageRead(
    VersionedKey<Guid> Id,
    [property: Sensible] Guid MessageId,
    [property: Sensible] Guid UserId)
{
    public PrivateMessageRead(Guid messageId, Guid userId)
        : this(new(Guid.Empty), messageId, userId) { }
}

public sealed record PrivateUserBlock(
    VersionedKey<Guid> Id,
    [property: Sensible] Guid UserId,
    [property: Sensible] Guid BlockedUserId,
    bool Active)
{
    public PrivateUserBlock(Guid userId, Guid blockedUserId, bool active)
        : this(new(Guid.Empty), userId, blockedUserId, active) { }
}
