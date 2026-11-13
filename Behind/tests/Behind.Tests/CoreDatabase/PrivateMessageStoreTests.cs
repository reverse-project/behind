using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Domain;
using Behind.Common.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Tests.CoreDatabase;

public sealed class PrivateMessageStoreTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Guid _sender;
    private readonly Guid _recipient;
    private readonly Guid _hidden;
    private readonly Guid _other;
    private readonly Guid _role;

    public PrivateMessageStoreTests()
    {
        _connection.Open();
        using var context = Context();
        context.Database.EnsureCreated();
        _sender = SeedUser(context, "sender@example.test");
        _recipient = SeedUser(context, "recipient@example.test");
        _hidden = SeedUser(context, "hidden@example.test");
        _other = SeedUser(context, "other@example.test");
        var role = new UserRole("members");
        context.Roles.Add(role);
        context.SaveChangesAsync().GetAwaiter().GetResult();
        _role = context.Entry(role).Property<Guid>("Id").CurrentValue;
    }

    [Fact]
    public async Task HiddenRepliesDoNotAffectOtherViewersOrExposeEvidence()
    {
        await using var context = Context(true);
        var store = new PrivateMessageStore(context);
        var created = await store.Create(_sender, "subject", "initial", [_recipient], [], [_hidden], [], true, false);
        Assert.True(created.Succeeded);
        var id = created.Id!.Value;
        var before = Assert.Single((await store.List(_recipient, "inbox")).Items);
        var reply = await store.Reply(_hidden, id, "secret");
        Assert.True(reply.Succeeded);
        var after = Assert.Single((await store.List(_recipient, "inbox")).Items);
        Assert.Equal(before, after);
        var visible = (await store.Find(_recipient, id))!;
        Assert.Single(visible.Messages);
        Assert.DoesNotContain(visible.Participants, x => x.Id == _hidden);
        Assert.Null(await store.FindEvidence(_recipient, reply.Id!.Value));
        Assert.Equal("secret", (await store.FindEvidence(_sender, reply.Id.Value))!.Content);
        Assert.Equal(2, (await store.Find(_hidden, id))!.Messages.Count);
        Assert.False((await store.Reply(_recipient, id, "probe", reply.Id)).Succeeded);
    }

    [Fact]
    public async Task RoleMembershipIsLiveAndMailboxStateIsPerUser()
    {
        await using var context = Context(true);
        var store = new PrivateMessageStore(context);
        Assert.False((await store.Create(_sender, "empty", "initial", [], [_role], [], [], true, false, true)).Succeeded);
        await AddRole(context, _other, _role);
        var created = await store.Create(_sender, "subject", "initial", [], [_role], [], [], true, false, true);
        var id = created.Id!.Value;
        Assert.Null(await store.Find(_recipient, id));
        await AddRole(context, _recipient, _role);
        Assert.Single((await store.Find(_recipient, id))!.Messages);
        Assert.True((await store.SetMailbox(_recipient, id, "read")).Succeeded);
        Assert.Single((await store.List(_recipient, "read")).Items);
        Assert.Single((await store.List(_other, "inbox")).Items);
        Assert.True((await store.SetMailbox(_recipient, id, "delete")).Succeeded);
        Assert.Single((await store.List(_recipient, "deleted")).Items);
        Assert.NotNull(await store.Find(_other, id));
        Assert.True((await store.SetMailbox(_recipient, id, "restore")).Succeeded);
        Assert.Single((await store.List(_recipient, "read")).Items);
        Assert.True((await store.Leave(_recipient, id)).Succeeded);
        Assert.Null(await store.Find(_recipient, id));
        Assert.True((await store.Join(_recipient, id)).Succeeded);
        Assert.NotNull(await store.Find(_recipient, id));
        var membership = await context.Set<Dictionary<string, object>>("UserRoles")
            .SingleAsync(x => EF.Property<Guid>(x, "UserId") == _recipient && EF.Property<Guid>(x, "RoleId") == _role);
        context.Remove(membership);
        await context.SaveChangesAsync();
        Assert.Null(await store.Find(_recipient, id));
    }

    [Fact]
    public async Task BlocksApplyBothWaysAndSystemRepliesArePrivate()
    {
        await using var context = Context(true);
        var store = new PrivateMessageStore(context);
        Assert.True((await store.SetBlock(_recipient, _sender, true)).Succeeded);
        Assert.False((await store.Create(_sender, "subject", "initial", [_recipient], [], [], [], false, false)).Succeeded);
        Assert.False((await store.Create(_recipient, "subject", "initial", [_sender], [], [], [], false, false)).Succeeded);
        var created = await store.Create(_sender, "notice", "initial", [_recipient, _hidden], [], [], [], true, true, true);
        Assert.True(created.Succeeded);
        var id = created.Id!.Value;
        var reply = await store.Reply(_recipient, id, "private response");
        Assert.True(reply.Succeeded);
        Assert.Single((await store.Find(_hidden, id))!.Messages);
        Assert.Equal(2, (await store.Find(_other, id, systemModerator: true))!.Messages.Count);
        Assert.Null(await store.Find(_other, id));
        Assert.False((await store.Reply(_other, id, "no", systemModerator: true)).Succeeded);
        Assert.True((await store.SetBlock(_recipient, _sender, false)).Succeeded);
        Assert.Empty(await store.Blocks(_recipient));
    }

    [Fact]
    public async Task ReportConversationIsRestrictedToReporterAndReportModerators()
    {
        await using var context = Context(true);
        var store = new PrivateMessageStore(context);
        var id = await store.CreateReport(_sender, "report", "reason");
        Assert.Null(await store.Find(_other, id, systemModerator: true));
        Assert.NotNull(await store.Find(_other, id, reportModerator: true));
        await store.AppendReportMessage(id, _other, "moderator response");
        Assert.Equal(2, (await store.Find(_sender, id))!.Messages.Count);
        Assert.True((await store.Reply(_other, id, "further", reportModerator: true)).Succeeded);
        Assert.Equal(3, (await store.Find(_sender, id))!.Messages.Count);
        Assert.Null(await store.Find(_recipient, id));
        await store.DeleteReportConversation(id, CancellationToken.None);
        Assert.Null(await store.Find(_sender, id));
    }

    [Fact]
    public async Task NewVisibleRepliesRestoreUnreadStateAndCrossThreadRepliesAreRejected()
    {
        await using var context = Context(true);
        var store = new PrivateMessageStore(context);
        var first = (await store.Create(_sender, "one", "one", [_recipient], [], [], [], false, false)).Id!.Value;
        var second = (await store.Create(_sender, "two", "two", [_recipient], [], [], [], false, false)).Id!.Value;
        var firstMessage = Assert.Single((await store.Find(_recipient, first))!.Messages);
        Assert.NotNull(firstMessage.CreatedAt);
        Assert.False((await store.Reply(_recipient, second, "bad", firstMessage.Id)).Succeeded);
        Assert.True((await store.SetMailbox(_recipient, first, "read")).Succeeded);
        Assert.Single((await store.List(_recipient, "read")).Items);
        Assert.True((await store.Reply(_sender, first, "new")).Succeeded);
        Assert.Empty((await store.List(_recipient, "read")).Items);
        Assert.Equal(2, (await store.List(_recipient, "inbox")).Total);
    }

    [Fact]
    public async Task ReportConversationOperationsParticipateInCallerTransaction()
    {
        await using var context = Context(true);
        var store = new PrivateMessageStore(context);
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var id = await store.CreateReport(_sender, "report", "reason");
            await store.AppendReportMessage(id, _other, "response");
            Assert.NotNull(await store.Find(_sender, id));
            await transaction.RollbackAsync();
        }

        context.ChangeTracker.Clear();
        Assert.Empty((await store.List(_sender, "sent")).Items);
        var ordinary = (await store.Create(_sender, "ordinary", "message", [_recipient], [], [], [], false, false)).Id!.Value;
        await store.DeleteReportConversation(ordinary, CancellationToken.None);
        Assert.NotNull(await store.Find(_sender, ordinary));
    }

    [Fact]
    public async Task HiddenRoleRepliesAreNeverExposedToOtherRoleMembers()
    {
        await using var context = Context(true);
        await AddRole(context, _hidden, _role);
        await AddRole(context, _other, _role);
        var store = new PrivateMessageStore(context);
        var id = (await store.Create(_sender, "role", "message", [_recipient], [], [], [_role], true, false, true)).Id!.Value;
        var reply = await store.Reply(_hidden, id, "role secret");
        Assert.True(reply.Succeeded);
        Assert.Single((await store.Find(_other, id))!.Messages);
        Assert.Null(await store.FindEvidence(_other, reply.Id!.Value));
        Assert.DoesNotContain((await store.Find(_recipient, id))!.Participants, x => x.Id == _role);
    }

    [Fact]
    public async Task ConcurrentInitialMailboxVersionsCannotBothPersist()
    {
        await using var context = Context(true);
        var store = new PrivateMessageStore(context);
        var conversation = (await store.Create(_sender, "subject", "initial", [_recipient], [], [], [], false, false)).Id!.Value;
        var firstId = Guid.NewGuid();
        var first = new PrivateMailbox(new(firstId, 1), conversation, _recipient, false, false);
        await context.PrivateMailboxes.AddAsync(first);
        context.Entry(first).StampVersionedKey(firstId, 1);
        await context.SaveChangesAsync();
        var secondId = Guid.NewGuid();
        var conflicting = new PrivateMailbox(new(secondId, 1), conversation, _recipient, true, false);
        await context.PrivateMailboxes.AddAsync(conflicting);
        context.Entry(conflicting).StampVersionedKey(secondId, 1);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task OwnerCanInvitePeersWhoCanAccessHistoryAndLeaveAndRejoin()
    {
        await using var context = Context(true);
        var store = new PrivateMessageStore(context);
        var id = (await store.Create(_sender, "group", "history", [_recipient], [], [], [], true, false)).Id!.Value;
        Assert.False((await store.Join(_other, id)).Succeeded);
        Assert.False((await store.Invite(_recipient, id, [_other], [], [], [])).Succeeded);
        Assert.True((await store.Invite(_sender, id, [], [], [_other], [])).Succeeded);
        Assert.Equal("history", Assert.Single((await store.Find(_other, id))!.Messages).Content);
        Assert.DoesNotContain((await store.Find(_recipient, id))!.Participants, x => x.Id == _other);
        Assert.False((await store.Invite(_sender, id, [_other], [], [], [])).Succeeded);
        Assert.True((await store.Leave(_sender, id)).Succeeded);
        Assert.Null(await store.Find(_sender, id));
        var left = Assert.Single((await store.List(_sender, "left")).Items);
        Assert.Equal(id, left.Id);
        Assert.Null(left.UpdatedAt);
        Assert.False((await store.Invite(_sender, id, [_hidden], [], [], [])).Succeeded);
        Assert.True((await store.Join(_sender, id)).Succeeded);
        Assert.True((await store.Invite(_sender, id, [_hidden], [], [], [])).Succeeded);
        Assert.True((await store.Leave(_other, id)).Succeeded);
        Assert.Null(await store.Find(_other, id));
        Assert.True((await store.Join(_other, id)).Succeeded);
        Assert.NotNull(await store.Find(_other, id));
    }

    [Fact]
    public async Task LatestVersionOnlyIsUsedForConversationsRecipientsAndMessages()
    {
        await using var context = Context(true);
        var store = new PrivateMessageStore(context);
        var id = (await store.Create(_sender, "old subject", "old message", [_recipient], [], [], [], true, false)).Id!.Value;
        var first = (await store.Find(_recipient, id))!;
        var firstMessage = Assert.Single(first.Messages);
        var conversation = new PrivateConversation(new(id, 2), _sender, "new subject", true, false, false);
        await context.PrivateConversations.AddAsync(conversation);
        context.Entry(conversation).StampVersionedKey(id, 2);
        var message = new PrivateMessage(new(firstMessage.Id, 2), id, _sender, "new message", null, null, true);
        await context.PrivateMessages.AddAsync(message);
        context.Entry(message).StampVersionedKey(firstMessage.Id, 2);
        var recipient = await context.PrivateConversationRecipients.SingleAsync(x => x.ConversationId == id);
        var recipientId = context.Entry(recipient).VersionedId.Key;
        var updatedRecipient = new PrivateConversationRecipient(new(recipientId, 2), id, _recipient, false, true);
        await context.PrivateConversationRecipients.AddAsync(updatedRecipient);
        context.Entry(updatedRecipient).StampVersionedKey(recipientId, 2);
        await context.SaveChangesAsync();
        var latest = (await store.Find(_recipient, id))!;
        Assert.Equal("new subject", latest.Subject);
        Assert.Equal("new message", Assert.Single(latest.Messages).Content);
        Assert.DoesNotContain(latest.Participants, x => x.Id == _recipient);
        Assert.Single((await store.List(_recipient, "inbox")).Items);
    }

    [Fact]
    public async Task InvitingRolesIsLiveAndExplicitBlockedUsersCannotBeInvited()
    {
        await using var context = Context(true);
        var store = new PrivateMessageStore(context);
        var id = (await store.Create(_sender, "group", "history", [_recipient], [], [], [], true, false)).Id!.Value;
        Assert.True((await store.SetBlock(_other, _sender, true)).Succeeded);
        Assert.False((await store.Invite(_sender, id, [_other], [], [], [])).Succeeded);
        Assert.False((await store.Create(_sender, "group", "initial", [_other, _hidden], [], [], [], true, false)).Succeeded);
        Assert.True((await store.Invite(_sender, id, [], [_role], [], [], systemModerator: true)).Succeeded);
        await AddRole(context, _hidden, _role);
        Assert.Equal("history", Assert.Single((await store.Find(_hidden, id))!.Messages).Content);
        var notice = (await store.Create(_sender, "system", "notice", [_recipient], [], [], [], true, true, true)).Id!.Value;
        Assert.False((await store.Invite(_sender, notice, [_hidden], [], [], [])).Succeeded);
        Assert.False((await store.Leave(_recipient, notice)).Succeeded);
        Assert.False((await store.Join(_sender, notice)).Succeeded);
    }

    [Fact]
    public async Task RecipientLookupReturnsAtMostTenUsersAndRolesAndSearchFindsItemsOutsideTheInitialResults()
    {
        await using var context = Context(true);
        for (var index = 0; index < 15; index++)
        {
            context.Users.Add(new HumanUser($"Lookup {index:D2}", [new Email($"lookup{index}@example.test")], None));
            context.Roles.Add(new UserRole($"Lookup {index:D2}"));
        }
        await context.SaveChangesAsync();
        var store = new PrivateMessageStore(context);
        var found = await store.Recipients("Lookup");
        Assert.Equal(10, found.Users.Count);
        Assert.Equal(10, found.Roles.Count);
        Assert.All(found.Users, user => Assert.DoesNotContain("Lookup 14", user.DisplayName));
        var repeated = await store.Recipients("Lookup");
        Assert.Equal(found.Users.Select(user => user.Id), repeated.Users.Select(user => user.Id));
        Assert.Equal(found.Roles.Select(role => role.Id), repeated.Roles.Select(role => role.Id));
        Assert.Contains("Lookup 14", Assert.Single((await store.Recipients("Lookup 14")).Users).DisplayName);
        Assert.Equal("Lookup 14", Assert.Single((await store.Recipients("Lookup 14")).Roles).DisplayName);
        var reports = await store.Recipients("Lookup", reportTargets: true);
        Assert.Equal(10, reports.Users.Count);
        Assert.Empty(reports.Roles);
        var browse = await store.Recipients(null);
        Assert.Equal(10, browse.Users.Count);
        Assert.Equal(10, browse.Roles.Count);
    }

    [Fact]
    public async Task LegacyTwoPersonChatsSupportGroupMembershipAndNewChatsAreAlwaysGroups()
    {
        await using var context = Context(true);
        var store = new PrivateMessageStore(context);
        var id = (await store.Create(_sender, "chat", "history", [_recipient], [], [], [], false, false)).Id!.Value;
        Assert.True((await store.Find(_sender, id))!.IsGroup);
        var legacy = new PrivateConversation(new(id, 2), _sender, "chat", false, false, false);
        await context.PrivateConversations.AddAsync(legacy);
        context.Entry(legacy).StampVersionedKey(id, 2);
        await context.SaveChangesAsync();
        Assert.True((await store.Find(_sender, id))!.IsGroup);
        Assert.True((await store.Leave(_recipient, id)).Succeeded);
        Assert.True(Assert.Single((await store.List(_recipient, "left")).Items).IsGroup);
        Assert.True((await store.Join(_recipient, id)).Succeeded);
        Assert.True((await store.Invite(_sender, id, [_other], [], [], [])).Succeeded);
        Assert.Equal("history", Assert.Single((await store.Find(_other, id))!.Messages).Content);
    }

    [Fact]
    public async Task RoleRecipientsAndInvitationsRequireModerationPermission()
    {
        await using var context = Context(true);
        var store = new PrivateMessageStore(context);
        Assert.Equal("forbidden", (await store.Create(_sender, "chat", "body", [_recipient], [_role], [], [], true, false)).Error);
        Assert.Equal("forbidden", (await store.Create(_sender, "chat", "body", [_recipient], [], [], [_role], true, false)).Error);
        var id = (await store.Create(_sender, "chat", "body", [_recipient], [], [], [], false, false)).Id!.Value;
        Assert.Equal("forbidden", (await store.Invite(_sender, id, [], [_role], [], [])).Error);
        Assert.Equal("forbidden", (await store.Invite(_sender, id, [], [], [], [_role])).Error);
    }

    [Fact]
    public async Task EmptyDisplayNamesUseStableUserLabels()
    {
        await using var context = Context(true);
        var user = new HumanUser("", [new Email("blank@example.test")], None);
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
        var userId = context.Entry(user).Property<Guid>("Id").CurrentValue;
        var store = new PrivateMessageStore(context);
        var id = (await store.Create(_sender, "label", "message", [userId], [], [], [], false, false)).Id!.Value;
        var expected = userId.ToUserLabel("");
        Assert.Equal(expected, (await store.Find(_sender, id))!.Participants.Single(x => x.Id == userId).DisplayName);
        Assert.Equal(expected, (await store.Recipients(null)).Users.Single(x => x.Id == userId).DisplayName);
        Assert.True((await store.SetBlock(_sender, userId, true)).Succeeded);
        Assert.Equal(expected, Assert.Single(await store.Blocks(_sender)).DisplayName);
    }

    private TestContext Context(bool audited = false)
    {
        var builder = new DbContextOptionsBuilder<TestContext>().UseSqlite(_connection);
        if (audited)
        {
            builder.AddInterceptors(new AuditSaveChangesInterceptor(
                () => new FixedAuditActorProvider(AuditActor.ForUser(_sender)), TimeProvider.System));
        }
        return new TestContext(builder.Options);
    }

    private static Guid SeedUser(TestContext context, string email)
    {
        var user = new HumanUser(email, [new Email(email)], None);
        context.Users.Add(user);
        context.SaveChangesAsync().GetAwaiter().GetResult();
        return context.Entry(user).Property<Guid>("Id").CurrentValue;
    }

    private static async Task AddRole(TestContext context, Guid userId, Guid roleId)
    {
        await context.Set<Dictionary<string, object>>("UserRoles").AddAsync(new() { ["UserId"] = userId, ["RoleId"] = roleId });
        await context.SaveChangesAsync();
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : AbstractCoreContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyCoreProviderConventions();
        }
    }

    public void Dispose() => _connection.Dispose();
}
