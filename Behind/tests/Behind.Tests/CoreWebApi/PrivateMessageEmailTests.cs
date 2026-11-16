using System.Net.Mail;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Domain;
using Behind.Core.WebApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Behind.Tests.CoreWebApi;

public sealed class PrivateMessageEmailTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static async Task<Guid> User(AbstractCoreContext context, string email, bool confirmed = true)
    {
        var user = new HumanUser("Person", [new Email(email, confirmed ? DateTimeOffset.UtcNow : null)],
            Behind.Common.FunctionalExtensions.None);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return context.Entry(user).Property<Guid>("Id").CurrentValue;
    }

    private static PrivateMessageEmailDispatcher Dispatcher(
        AbstractCoreContext context, IModuleEmailSender sender, string? url = "https://example.test/app/")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Frontend:Url"] = url }).Build();
        return new(context, new(context), new(context), sender, configuration,
            Substitute.For<ILogger<PrivateMessageEmailDispatcher>>());
    }

    [Fact]
    public async Task ComposeAndRepliesNotifyOnlyTheirAudienceIncludingHiddenAndLiveRoleRecipients()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var author = await User(context, "author@example.test");
        var visible = await User(context, "visible@example.test");
        var hidden = await User(context, "hidden@example.test");
        var member = await User(context, "role@example.test");
        var unconfirmed = await User(context, "unconfirmed@example.test", false);
        var role = new UserRole("Recipients");
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        var roleId = context.Entry(role).Property<Guid>("Id").CurrentValue;
        await context.Set<Dictionary<string, object>>("UserRoles").AddAsync(new() { ["UserId"] = member, ["RoleId"] = roleId });
        await context.SaveChangesAsync();
        var store = new PrivateMessageStore(context);
        var id = (await store.Create(author, "Secret subject", "Secret content", [visible, unconfirmed], [roleId], [hidden], [],
            true, false, true)).Id!.Value;
        var initial = Assert.Single((await store.Find(author, id))!.Messages).Id;
        var deliveries = await new PrivateMessageEmailStore(context).Pending(default);
        Assert.Equal(3, deliveries.Count);
        Assert.Equal(new[] { "hidden@example.test", "role@example.test", "visible@example.test" },
            deliveries.Select(delivery => delivery.Email).Order().ToArray());
        Assert.All(deliveries, delivery => Assert.Equal(initial, delivery.MessageId));
        Assert.True((await store.Reply(hidden, id, "Hidden reply", initial)).Succeeded);
        var queued = await new PrivateMessageEmailStore(context).Pending(default);
        var hiddenReply = Assert.Single(queued, delivery => delivery.MessageId != initial);
        Assert.Equal(author, hiddenReply.UserId);
        Assert.True((await store.Leave(member, id)).Succeeded);
        Assert.True((await store.Reply(visible, id, "Public reply")).Succeeded);
        var latestMessage = (await store.Find(author, id))!.Messages.Last().Id;
        var publicAudience = (await new PrivateMessageEmailStore(context).Pending(default))
            .Where(delivery => delivery.MessageId == latestMessage).Select(delivery => delivery.UserId).Order().ToArray();
        Assert.Equal(new[] { author, hidden }.Order().ToArray(), publicAudience);
    }

    [Fact]
    public async Task SuccessfulEmailsContainOnlyTheDeepLinkAndAreNeverSentTwice()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var author = await User(context, "author@example.test");
        var recipient = await User(context, "recipient@example.test");
        var id = (await new PrivateMessageStore(context).Create(author, "Secret subject", "Secret content", [recipient],
            [], [], [], true, false)).Id!.Value;
        var sender = Substitute.For<IModuleEmailSender>();
        var dispatcher = Dispatcher(context, sender);
        await dispatcher.Deliver(default);
        await dispatcher.Deliver(default);
        await sender.Received(1).SendEmailAsync("recipient@example.test", "New private message",
            Arg.Is<string>(body => body.Contains($"https://example.test/app/#/private-messages/{id}")
                && !body.Contains("Secret") && !body.Contains("author@example.test")), Arg.Any<CancellationToken>());
        var delivery = await context.Set<PrivateMessageEmailDelivery>().LatestOf(context.Set<PrivateMessageEmailDelivery>()).SingleAsync();
        Assert.Equal("sent", delivery.Status);
        Assert.Equal(1, delivery.Attempts);
        Assert.Equal(3, context.Entry(delivery).VersionedId.Version);
    }

    [Fact]
    public async Task KnownRejectionRetriesOriginalAddressButUncertainSendingDoesNotRetry()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var author = await User(context, "author@example.test");
        var recipient = await User(context, "original@example.test");
        var store = new PrivateMessageStore(context);
        await store.Create(author, "Chat", "Body", [recipient], [], [], [], true, false);
        var sender = Substitute.For<IModuleEmailSender>();
        sender.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new SmtpException(SmtpStatusCode.MailboxBusy)), Task.CompletedTask);
        var dispatcher = Dispatcher(context, sender);
        await dispatcher.Deliver(default);
        Assert.Equal("failed", Assert.Single(await new PrivateMessageEmailStore(context).Pending(default)).Status);
        var original = await context.Emails.SingleAsync(email => email.Value == "original@example.test");
        context.Emails.Remove(original);
        context.Add(new Email("changed@example.test", DateTimeOffset.UtcNow)).Property<Guid>("UserId").CurrentValue = recipient;
        await context.SaveChangesAsync();
        await dispatcher.Deliver(default);
        await sender.Received(2).SendEmailAsync("original@example.test", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await store.Create(author, "Chat", "Another body", [recipient], [], [], [], true, false);
        sender.ClearReceivedCalls();
        sender.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new SmtpException("Connection interrupted")));
        await Assert.ThrowsAsync<SmtpException>(() => dispatcher.Deliver(default));
        await dispatcher.Deliver(default);
        await sender.Received(1).SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Contains(await context.Set<PrivateMessageEmailDelivery>().LatestOf(context.Set<PrivateMessageEmailDelivery>()).ToListAsync(),
            delivery => delivery.Status == "sending");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("/relative")]
    [InlineData("https://user:password@example.test/")]
    [InlineData("https://example.test/?query=1")]
    [InlineData("https://example.test/#fragment")]
    public async Task InvalidFrontendUrlLeavesPendingAndLeavingBeforeDeliveryCancelsNotification(string? url)
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var author = await User(context, "author@example.test");
        var recipient = await User(context, "recipient@example.test");
        var store = new PrivateMessageStore(context);
        var id = (await store.Create(author, "Chat", "Body", [recipient], [], [], [], true, false)).Id!.Value;
        var sender = Substitute.For<IModuleEmailSender>();
        await Dispatcher(context, sender, url).Deliver(default);
        Assert.Single(await new PrivateMessageEmailStore(context).Pending(default));
        await store.Leave(recipient, id);
        await Dispatcher(context, sender).Deliver(default);
        await sender.DidNotReceive().SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Equal("cancelled", (await context.Set<PrivateMessageEmailDelivery>()
            .LatestOf(context.Set<PrivateMessageEmailDelivery>()).SingleAsync()).Status);
    }

    [Fact]
    public async Task KnownFailuresStopAfterThreeAttemptsAndSystemRepliesEmailOnlyTheOriginalSender()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var author = await User(context, "author@example.test");
        var recipient = await User(context, "recipient@example.test");
        var other = await User(context, "other@example.test");
        var store = new PrivateMessageStore(context);
        var id = (await store.Create(author, "System", "Body", [recipient, other], [], [], [], true, true, true)).Id!.Value;
        var sender = Substitute.For<IModuleEmailSender>();
        sender.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new SmtpException(SmtpStatusCode.MailboxBusy)));
        var dispatcher = Dispatcher(context, sender);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await dispatcher.Deliver(default);
        }
        await sender.Received(6).SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Empty(await new PrivateMessageEmailStore(context).Pending(default));
        await store.Reply(recipient, id, "Private system reply");
        Assert.Equal(author, Assert.Single(await new PrivateMessageEmailStore(context).Pending(default)).UserId);
    }

    [Fact]
    public async Task ReportModeratorRepliesNotifyOnlyTheReporterAndRemovedRoleMembershipCancelsQueuedDelivery()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var reporter = await User(context, "reporter@example.test");
        var moderator = await User(context, "moderator@example.test");
        var member = await User(context, "role@example.test");
        var store = new PrivateMessageStore(context);
        var report = await store.CreateReport(reporter, "Report", "Private report");
        await store.AppendReportMessage(report, moderator, "Private reply");
        Assert.Equal(reporter, Assert.Single(await new PrivateMessageEmailStore(context).Pending(default)).UserId);
        var role = new UserRole("Role");
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        var roleId = context.Entry(role).Property<Guid>("Id").CurrentValue;
        var membership = new Dictionary<string, object> { ["UserId"] = member, ["RoleId"] = roleId };
        context.Set<Dictionary<string, object>>("UserRoles").Add(membership);
        await context.SaveChangesAsync();
        await store.Create(moderator, "Chat", "Body", [], [roleId], [], [], true, false, true);
        context.Set<Dictionary<string, object>>("UserRoles").Remove(membership);
        await context.SaveChangesAsync();
        var sender = Substitute.For<IModuleEmailSender>();
        await Dispatcher(context, sender).Deliver(default);
        await sender.Received(1).SendEmailAsync("reporter@example.test", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await sender.DidNotReceive().SendEmailAsync("role@example.test", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConcurrentDispatchersDoNotSendAClaimedNotificationAgainAndCancellationRemainsUncertain()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var author = await User(context, "author@example.test");
        var recipient = await User(context, "recipient@example.test");
        await new PrivateMessageStore(context).Create(author, "Chat", "Body", [recipient], [], [], [], true, false);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = Substitute.For<IModuleEmailSender>();
        sender.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                started.SetResult();
                return cancelled.Task;
            });
        var first = Dispatcher(context, sender).Deliver(default);
        await started.Task;
        await using var other = _factory.CreateAuditedContext(AuditActor.Anonymous);
        await Dispatcher(other, sender).Deliver(default);
        cancelled.SetCanceled();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Dispatcher(other, sender).Deliver(default);
        await sender.Received(1).SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidPersistedDeliveryStatesAreRejectedExplicitly()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var store = new PrivateMessageEmailStore(context);
        store.Stage(new(new(Guid.NewGuid(), 1), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "recipient@example.test", "invalid", 0));
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Pending(default));
    }
}
