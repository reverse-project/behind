using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.WebApi.EmailSender;
using Behind.Core.WebApi.Identity;
using Behind.Core.WebApi.MagicLinks;
using Behind.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi.MagicLinks;

public class MagicLinkLoggingTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(10);
    private static readonly TimeSpan Overlap = TimeSpan.FromDays(2);

    private static MagicLinkMail NewMail() =>
        new(new ApplicationUser { DisplayName = "User" }, "user@test.com", "https://app.test/link");

    [Fact]
    public async Task MailService_SentMail_LogsInformationWithUserId()
    {
        var queue = new MagicLinkMailQueue();
        var logger = new CapturingLogger<MagicLinkMailService>();
        using var service = new MagicLinkMailService(queue, Substitute.For<IMagicLinkEmailSender>().InScopeFactory(), logger);
        var mail = NewMail();

        await service.StartAsync(CancellationToken.None);
        queue.TryEnqueue(mail);
        await logger.WaitForAsync(LogLevel.Information).WaitAsync(Timeout);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(mail.User.Id, logger.Single(LogLevel.Information).State["UserId"]);
        Assert.Equal(mail.User.Id, logger.Single(LogLevel.Debug).State["UserId"]);
    }

    [Fact]
    public async Task MailService_SenderFailure_LogsErrorWithUserId()
    {
        var queue = new MagicLinkMailQueue();
        var sender = Substitute.For<IMagicLinkEmailSender>();
        sender.SendMagicLinkAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("smtp")));
        var logger = new CapturingLogger<MagicLinkMailService>();
        using var service = new MagicLinkMailService(queue, sender.InScopeFactory(), logger);
        var mail = NewMail();

        await service.StartAsync(CancellationToken.None);
        queue.TryEnqueue(mail);
        await logger.WaitForAsync(LogLevel.Error).WaitAsync(Timeout);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(mail.User.Id, logger.Single(LogLevel.Error).State["UserId"]);
        Assert.False(logger.Has(LogLevel.Information));
    }

    private static MagicLinkCleanupService CleanupReturning(
        int deleted,
        CapturingLogger<MagicLinkCleanupService> logger)
    {
        var commands = Substitute.For<IMagicLinkCommands>();
        commands.DeleteExpired(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(deleted);
        var services = new ServiceCollection().AddScoped(_ => commands).BuildServiceProvider();

        return new MagicLinkCleanupService(
            services.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            new MagicLinkSettingsStore(Some(new MagicLinkSettings(Uuid.Empty, None))),
            logger);
    }

    [Theory]
    [InlineData(3, LogLevel.Information)]
    [InlineData(0, LogLevel.Debug)]
    public async Task Cleanup_LogsDeletedCountAtMatchingLevel(int deleted, LogLevel expected)
    {
        var logger = new CapturingLogger<MagicLinkCleanupService>();
        using var service = CleanupReturning(deleted, logger);

        await service.StartAsync(CancellationToken.None);
        await logger.WaitForAsync(expected).WaitAsync(Timeout);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(deleted, logger.Single(expected).State["DeletedCount"]);
    }

    private static MagicLinkSecretRotator RotatorWith(
        Option<MagicLinkSettings> settings,
        TimeProvider time,
        CapturingLogger<MagicLinkSecretRotator> logger) =>
        new(
            new MagicLinkSettingsStore(settings),
            Substitute.For<IMagicLinkSettingsPersister>(),
            time,
            logger);

    private static MagicLinkSettings SettingsCreatedAt(DateTimeOffset created) =>
        new(Uuid.CreateVersion7(created), default, Some(Interval), Some(Overlap));

    [Fact]
    public async Task Rotator_WithoutSettings_LogsDebug()
    {
        var logger = new CapturingLogger<MagicLinkSecretRotator>();

        await RotatorWith(None, new FixedTimeProvider(Now), logger).RotateIfNeededAsync();

        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task Rotator_SecretsStillCurrent_LogsDebug()
    {
        var logger = new CapturingLogger<MagicLinkSecretRotator>();

        await RotatorWith(Some(SettingsCreatedAt(Now)), new FixedTimeProvider(Now), logger).RotateIfNeededAsync();

        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task Rotator_SecretsDue_LogsInformation()
    {
        var logger = new CapturingLogger<MagicLinkSecretRotator>();

        await RotatorWith(
            Some(SettingsCreatedAt(Now)),
            new FixedTimeProvider(Now + Interval),
            logger).RotateIfNeededAsync();

        Assert.True(logger.Has(LogLevel.Information));
        Assert.False(logger.Has(LogLevel.Error));
    }
}
