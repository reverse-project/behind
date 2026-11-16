namespace Behind.Core.Domain;

public sealed record PrivateMessagingRecipient(Guid Id, bool IsRole, bool IsHidden);

public sealed record PrivateMessagingBlock(Guid UserId, Guid BlockedUserId);

public sealed record PrivateMessagingViewer(
    Guid UserId,
    IReadOnlySet<Guid> RoleIds,
    bool CanModerateSystemMessages,
    bool CanModerateReports = false);

public sealed record PrivateMessagingConversation(Guid Id, Guid SenderId, bool IsSystem, bool IsReport = false);

public sealed record PrivateMessagingMessage(
    Guid Id,
    Guid ConversationId,
    Guid AuthorId,
    Guid? PrivateRecipientId,
    bool IsInitial);

public static class PrivateMessagingVisibility
{
    public static bool CanSendDirect(
        Guid senderId,
        Guid recipientId,
        bool isSystem,
        IReadOnlyCollection<PrivateMessagingBlock> blocks) =>
        isSystem || !blocks.Any(block =>
            block.UserId == senderId && block.BlockedUserId == recipientId
            || block.UserId == recipientId && block.BlockedUserId == senderId);

    public static bool CanReadConversation(
        PrivateMessagingConversation conversation,
        IReadOnlyCollection<PrivateMessagingRecipient> recipients,
        PrivateMessagingViewer viewer) =>
        viewer.UserId == conversation.SenderId
        || conversation.IsSystem && viewer.CanModerateSystemMessages
        || conversation.IsReport && viewer.CanModerateReports
        || recipients.Any(recipient => Matches(recipient, viewer));

    public static bool CanReadMessage(
        PrivateMessagingConversation conversation,
        IReadOnlyCollection<PrivateMessagingRecipient> recipients,
        PrivateMessagingMessage message,
        PrivateMessagingViewer viewer)
    {
        if (message.ConversationId != conversation.Id || !CanReadConversation(conversation, recipients, viewer))
        {
            return false;
        }

        if (conversation.IsSystem && message.PrivateRecipientId is null
            && (!message.IsInitial || message.AuthorId != conversation.SenderId))
        {
            return false;
        }

        return message.PrivateRecipientId is null
               || viewer.UserId == conversation.SenderId
               || viewer.UserId == message.PrivateRecipientId
               || conversation.IsSystem && viewer.CanModerateSystemMessages
               || conversation.IsReport && viewer.CanModerateReports;
    }

    public static bool TryGetReplyRecipient(
        PrivateMessagingConversation conversation,
        IReadOnlyCollection<PrivateMessagingRecipient> recipients,
        PrivateMessagingViewer viewer,
        PrivateMessagingMessage? replyTo,
        out Guid? privateRecipientId)
    {
        privateRecipientId = null;

        if (viewer.UserId != conversation.SenderId
            && !(conversation.IsReport && viewer.CanModerateReports)
            && !recipients.Any(recipient => Matches(recipient, viewer))
            || replyTo is not null && !CanReadMessage(conversation, recipients, replyTo, viewer))
        {
            return false;
        }

        if (replyTo?.PrivateRecipientId is { } inheritedRecipient)
        {
            privateRecipientId = inheritedRecipient;
            return true;
        }

        if (viewer.UserId == conversation.SenderId)
        {
            return !conversation.IsSystem;
        }

        var hidden = recipients.Any(recipient => recipient.IsHidden && Matches(recipient, viewer));
        if (conversation.IsSystem || hidden)
        {
            privateRecipientId = viewer.UserId;
        }

        return true;
    }

    public static bool CanExposeRecipient(PrivateMessagingRecipient recipient) => !recipient.IsHidden;

    private static bool Matches(PrivateMessagingRecipient recipient, PrivateMessagingViewer viewer) =>
        recipient.IsRole ? viewer.RoleIds.Contains(recipient.Id) : recipient.Id == viewer.UserId;
}
