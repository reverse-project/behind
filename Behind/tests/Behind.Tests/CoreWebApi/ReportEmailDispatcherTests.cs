using System.Net.Mail;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Domain;
using Behind.Core.WebApi;
using Behind.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace Behind.Tests.CoreWebApi;

public sealed class ReportEmailDispatcherTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SuccessfulDeliveryIsVersionedEncodedAndNotSentTwice()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var reports = new ReportStore(context);
        var id = Guid.NewGuid();
        reports.StageDelivery(new(new(Guid.NewGuid(), 1), id, Guid.NewGuid(), "target@example.test", "<script>\nhello", "pending"));
        await context.SaveChangesAsync();
        var sender = Substitute.For<IModuleEmailSender>();
        var dispatcher = new ReportEmailDispatcher(context, reports, sender, TimeProvider.System,
            Substitute.For<ILogger<ReportEmailDispatcher>>());

        Assert.Equal("sent", await dispatcher.Deliver(id, default));
        Assert.Equal("sent", await dispatcher.Deliver(id, default));
        await sender.Received(1).SendEmailAsync("target@example.test", "Moderation notification",
            "<p>&lt;script&gt;<br>hello</p>", Arg.Any<CancellationToken>());
        Assert.Equal(3, Assert.Single(await reports.Deliveries(id, default)).Id.Version);
    }

    [Fact]
    public async Task DeliveryFailureRemainsVisibleAndRetryUsesOriginalAddress()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var reports = new ReportStore(context);
        var id = Guid.NewGuid();
        reports.StageDelivery(new(new(Guid.NewGuid(), 1), id, Guid.NewGuid(), "original@example.test", "Notice", "pending"));
        await context.SaveChangesAsync();
        var sender = Substitute.For<IModuleEmailSender>();
        sender.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new SmtpException("Unavailable")), Task.CompletedTask);
        var dispatcher = new ReportEmailDispatcher(context, reports, sender, TimeProvider.System,
            Substitute.For<ILogger<ReportEmailDispatcher>>());

        Assert.Equal("failed", await dispatcher.Deliver(id, default));
        Assert.Equal("sent", await dispatcher.Deliver(id, default));
        await sender.Received(2).SendEmailAsync("original@example.test", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownInterruptedSendingIsNeverAutomaticallyRetried()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var reports = new ReportStore(context);
        var id = Guid.NewGuid();
        reports.StageDelivery(new(new(Guid.NewGuid(), 1), id, Guid.NewGuid(), "target@example.test", "Notice", "sending"));
        await context.SaveChangesAsync();
        var sender = Substitute.For<IModuleEmailSender>();
        var dispatcher = new ReportEmailDispatcher(context, reports, sender, TimeProvider.System,
            Substitute.For<ILogger<ReportEmailDispatcher>>());

        Assert.Equal("sending", await dispatcher.Deliver(id, default));
        await sender.DidNotReceive().SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
