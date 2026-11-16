using Behind.Core.Domain;

namespace Behind.Tests.Domain;

public sealed class PrivateMessagingVisibilityTests
{
    private static readonly Guid Sender = Guid.NewGuid();
    private static readonly Guid Visible = Guid.NewGuid();
    private static readonly Guid Hidden = Guid.NewGuid();
    private static readonly Guid Role = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid ConversationId = Guid.NewGuid();
    private static readonly PrivateMessagingConversation Conversation = new(ConversationId, Sender, false);
    private static readonly PrivateMessagingRecipient[] Recipients =
    [
        new(Visible, false, false),
        new(Hidden, false, true),
        new(Role, true, true)
    ];
    private static readonly PrivateMessagingMessage Root = new(Guid.NewGuid(), ConversationId, Sender, null, true);

    private static PrivateMessagingViewer Viewer(Guid id, bool moderator = false, params Guid[] roles) =>
        new(id, roles.ToHashSet(), moderator);

    [Fact]
    public void DirectBlocksWorkBothWaysButNotForSystemNotices()
    {
        PrivateMessagingBlock[] blocks = [new(Sender, Visible)];
        Assert.False(PrivateMessagingVisibility.CanSendDirect(Sender, Visible, false, blocks));
        Assert.False(PrivateMessagingVisibility.CanSendDirect(Visible, Sender, false, blocks));
        Assert.True(PrivateMessagingVisibility.CanSendDirect(Sender, Visible, true, blocks));
        Assert.True(PrivateMessagingVisibility.CanSendDirect(Sender, Other, false, blocks));
    }

    [Fact]
    public void LiveRoleMembershipControlsEarlierSharedMessages()
    {
        Assert.True(PrivateMessagingVisibility.CanReadMessage(Conversation, Recipients, Root, Viewer(Other, false, Role)));
        Assert.False(PrivateMessagingVisibility.CanReadMessage(Conversation, Recipients, Root, Viewer(Other)));
    }

    [Fact]
    public void HiddenRepliesAreVisibleOnlyToTheirAuthorAndSender()
    {
        Assert.True(PrivateMessagingVisibility.TryGetReplyRecipient(
            Conversation, Recipients, Viewer(Hidden), Root, out var audience));
        Assert.Equal(Hidden, audience);

        var reply = new PrivateMessagingMessage(Guid.NewGuid(), ConversationId, Hidden, audience, false);
        Assert.True(PrivateMessagingVisibility.CanReadMessage(Conversation, Recipients, reply, Viewer(Hidden)));
        Assert.True(PrivateMessagingVisibility.CanReadMessage(Conversation, Recipients, reply, Viewer(Sender)));
        Assert.False(PrivateMessagingVisibility.CanReadMessage(Conversation, Recipients, reply, Viewer(Visible)));
        Assert.False(PrivateMessagingVisibility.CanReadMessage(Conversation, Recipients, reply, Viewer(Other, true, Role)));
    }

    [Fact]
    public void HiddenRoleRepliesRemainPrivateAfterRoleMembershipChanges()
    {
        var member = Viewer(Other, false, Role);
        Assert.True(PrivateMessagingVisibility.TryGetReplyRecipient(Conversation, Recipients, member, Root, out var audience));
        Assert.Equal(Other, audience);
        var reply = new PrivateMessagingMessage(Guid.NewGuid(), ConversationId, Other, audience, false);

        Assert.False(PrivateMessagingVisibility.CanReadMessage(Conversation, Recipients, reply, Viewer(Hidden)));
        Assert.False(PrivateMessagingVisibility.CanReadMessage(Conversation, Recipients, reply, Viewer(Other)));
    }

    [Fact]
    public void OverlappingPublicAndHiddenMembershipKeepsReplyPrivate()
    {
        Assert.True(PrivateMessagingVisibility.TryGetReplyRecipient(
            Conversation, Recipients, Viewer(Visible, false, Role), Root, out var audience));
        Assert.Equal(Visible, audience);
    }

    [Fact]
    public void PrivateReplyChainsPreserveTheirAudience()
    {
        var reply = new PrivateMessagingMessage(Guid.NewGuid(), ConversationId, Hidden, Hidden, false);
        Assert.True(PrivateMessagingVisibility.TryGetReplyRecipient(
            Conversation, Recipients, Viewer(Sender), reply, out var audience));
        Assert.Equal(Hidden, audience);
        Assert.False(PrivateMessagingVisibility.TryGetReplyRecipient(
            Conversation, Recipients, Viewer(Visible), reply, out _));
    }

    [Fact]
    public void RepliesCannotReferToAnotherConversation()
    {
        var otherMessage = Root with { ConversationId = Guid.NewGuid() };
        Assert.False(PrivateMessagingVisibility.TryGetReplyRecipient(
            Conversation, Recipients, Viewer(Visible), otherMessage, out _));
    }

    [Fact]
    public void SystemRepliesArePrivateAndModeratorsMayReadButNotImpersonateParticipants()
    {
        var system = Conversation with { IsSystem = true };
        Assert.True(PrivateMessagingVisibility.TryGetReplyRecipient(
            system, Recipients, Viewer(Visible), Root, out var audience));
        Assert.Equal(Visible, audience);
        var reply = new PrivateMessagingMessage(Guid.NewGuid(), ConversationId, Visible, audience, false);

        Assert.True(PrivateMessagingVisibility.CanReadMessage(system, Recipients, reply, Viewer(Other, true)));
        Assert.False(PrivateMessagingVisibility.CanReadMessage(system, Recipients, reply, Viewer(Hidden)));
        Assert.False(PrivateMessagingVisibility.TryGetReplyRecipient(system, Recipients, Viewer(Other, true), reply, out _));
        Assert.False(PrivateMessagingVisibility.TryGetReplyRecipient(system, Recipients, Viewer(Sender), Root, out _));
        Assert.True(PrivateMessagingVisibility.TryGetReplyRecipient(system, Recipients, Viewer(Sender), reply, out var senderAudience));
        Assert.Equal(Visible, senderAudience);
    }

    [Fact]
    public void SystemRepliesWithoutPrivateAudienceAreNeverVisible()
    {
        var system = Conversation with { IsSystem = true };
        var invalid = new PrivateMessagingMessage(Guid.NewGuid(), ConversationId, Visible, null, false);
        Assert.False(PrivateMessagingVisibility.CanReadMessage(system, Recipients, invalid, Viewer(Sender)));
        Assert.False(PrivateMessagingVisibility.CanReadMessage(system, Recipients, invalid, Viewer(Other, true)));
        Assert.False(PrivateMessagingVisibility.CanReadMessage(
            system, Recipients, invalid with { IsInitial = true }, Viewer(Visible)));
    }

    [Fact]
    public void HiddenParticipantsNeverAppearInParticipantProjection()
    {
        Assert.Equal([Visible], Recipients.Where(PrivateMessagingVisibility.CanExposeRecipient).Select(recipient => recipient.Id));
    }
}
