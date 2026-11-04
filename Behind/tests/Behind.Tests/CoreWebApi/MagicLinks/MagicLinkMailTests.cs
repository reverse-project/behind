using Behind.Core.WebApi.EmailSender;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.MagicLinks;
using Behind.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace Behind.Tests.CoreWebApi.MagicLinks;

public class MagicLinkMailTests
{
    private static MagicLinkMail NewMail(string email = "user@test.com") =>
        new(new ApplicationUser { DisplayName = "User" }, email, "https://app.test/link");

    [Fact]
    public async Task Send_RoutesByTheFunctionOfTheMail()
    {
        var sender = Substitute.For<IMagicLinkEmailSender>();
        var user = new ApplicationUser { DisplayName = "User" };

        await sender.Send(new MagicLinkMail(user, "user@test.com", "link", MagicLinkFunction.LostPassword));
        await sender.Send(new MagicLinkMail(user, "user@test.com", "link", MagicLinkFunction.SignIn));

        await sender.Received(1).SendLostPasswordLinkAsync(user, "user@test.com", "link");
        await sender.Received(1).SendMagicLinkAsync(user, "user@test.com", "link");
    }

    [Fact]
    public void Queue_ReturnsEnqueuedMailsInOrder()
    {
        var queue = new MagicLinkMailQueue();
        var first = NewMail("a@test.com");
        var second = NewMail("b@test.com");

        Assert.True(queue.TryEnqueue(first));
        Assert.True(queue.TryEnqueue(second));

        Assert.True(queue.Reader.TryRead(out var readFirst));
        Assert.True(queue.Reader.TryRead(out var readSecond));
        Assert.Equal([first, second], [readFirst, readSecond]);
    }

    [Fact]
    public void Queue_WhenFull_RejectsInsteadOfGrowing()
    {
        var queue = new MagicLinkMailQueue();

        var accepted = Enumerable.Range(0, MagicLinkMailQueue.Capacity + 5)
            .Count(_ => queue.TryEnqueue(NewMail()));

        Assert.Equal(MagicLinkMailQueue.Capacity, accepted);
    }

    [Fact]
    public async Task Service_SendsQueuedMailsInBackground()
    {
        var queue = new MagicLinkMailQueue();
        var sender = Substitute.For<IMagicLinkEmailSender>();
        var sent = new TaskCompletionSource();
        sender.SendMagicLinkAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(_ =>
            {
                sent.SetResult();
                return Task.CompletedTask;
            });
        using var service = new MagicLinkMailService(queue, sender.InScopeFactory(), Substitute.For<ILogger<MagicLinkMailService>>());
        var mail = NewMail();

        await service.StartAsync(CancellationToken.None);
        queue.TryEnqueue(mail);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        await sender.Received(1).SendMagicLinkAsync(mail.User, mail.Email, mail.Link);
    }

    [Fact]
    public async Task Service_SenderFailure_IsLoggedAndDoesNotStopProcessing()
    {
        var queue = new MagicLinkMailQueue();
        var sender = Substitute.For<IMagicLinkEmailSender>();
        var second = new TaskCompletionSource();
        sender.SendMagicLinkAsync(Arg.Any<ApplicationUser>(), "fail@test.com", Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("smtp")));
        sender.SendMagicLinkAsync(Arg.Any<ApplicationUser>(), "ok@test.com", Arg.Any<string>())
            .Returns(_ =>
            {
                second.SetResult();
                return Task.CompletedTask;
            });
        var logger = EnabledLogger.For<MagicLinkMailService>();
        using var service = new MagicLinkMailService(queue, sender.InScopeFactory(), logger);

        await service.StartAsync(CancellationToken.None);
        queue.TryEnqueue(NewMail("fail@test.com"));
        queue.TryEnqueue(NewMail("ok@test.com"));
        await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<InvalidOperationException>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
