using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.EmailSender;
using Behind.Core.WebApi.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.EmailSender;

public class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendEmailAsync_WithUnreachableServer_PropagatesFailureAfterLoggingAttempt()
    {
        // Arrange
        var logger = EnabledLogger.For<SmtpEmailSender>();
        var configuration = new SmtpEmailConfiguration(
            "127.0.0.1",
            1,
            new Ssl(true),
            Some((Authentication)new BasicAuthentication("user@example.com", "password")),
            None
        );
        var options = Options.Create(configuration);
        var sender = new SmtpEmailSender(logger, options);

        const string email = "recipient@example.com";
        const string subject = "Test Subject";
        const string htmlMessage = "<p>Test message</p>";

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => sender.SendEmailAsync(email, subject, htmlMessage));

        // Assert
        logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
        logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task SendEmailAsync_WithNoAuthenticationAndNoFrom_ThrowsBecauseSenderIsUnknown()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SmtpEmailSender>>();
        var configuration = new SmtpEmailConfiguration(
            "127.0.0.1",
            1,
            new Ssl(true),
            new NoAuthentication(),
            None
        );
        var options = Options.Create(configuration);
        var sender = new SmtpEmailSender(logger, options);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendEmailAsync("recipient@example.com", "Test Subject", "<p>Test message</p>"));
        Assert.Contains("sender address", exception.Message);
    }

    [Fact]
    public async Task SendConfirmationLinkAsync_CreatesCorrectEmailContent()
    {
        // Arrange
        var logger = EnabledLogger.For<SmtpEmailSender>();
        var configuration = new SmtpEmailConfiguration(
            "127.0.0.1",
            1,
            new Ssl(true),
            Some((Authentication)new BasicAuthentication("user@example.com", "password")),
            None
        );
        var options = Options.Create(configuration);
        var sender = new SmtpEmailSender(logger, options);
        var user = new ApplicationUser { DisplayName = "Test User" };
        const string confirmationLink = "https://example.com/confirm?token=abc123";

        // Act
        await Assert.ThrowsAnyAsync<Exception>(
            () => sender.SendConfirmationLinkAsync(user, "test@example.com", confirmationLink));

        // Assert - Should log regardless of SMTP status
        logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task SendPasswordResetCodeAsync_CreatesCorrectEmailContent()
    {
        // Arrange
        var logger = EnabledLogger.For<SmtpEmailSender>();
        var configuration = new SmtpEmailConfiguration(
            "127.0.0.1",
            1,
            new Ssl(true),
            Some((Authentication)new BasicAuthentication("user@example.com", "password")),
            None
        );
        var options = Options.Create(configuration);
        var sender = new SmtpEmailSender(logger, options);
        var user = new ApplicationUser { DisplayName = "Test User" };
        const string resetCode = "123456";

        // Act
        await Assert.ThrowsAnyAsync<Exception>(
            () => sender.SendPasswordResetCodeAsync(user, "test@example.com", resetCode));

        // Assert
        logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task SendPasswordResetLinkAsync_CreatesCorrectEmailContent()
    {
        // Arrange
        var logger = EnabledLogger.For<SmtpEmailSender>();
        var configuration = new SmtpEmailConfiguration(
            "127.0.0.1",
            1,
            new Ssl(true),
            Some((Authentication)new BasicAuthentication("user@example.com", "password")),
            None
        );
        var options = Options.Create(configuration);
        var sender = new SmtpEmailSender(logger, options);
        var user = new ApplicationUser { DisplayName = "Test User" };
        const string resetLink = "https://example.com/reset?token=xyz789";

        // Act
        await Assert.ThrowsAnyAsync<Exception>(
            () => sender.SendPasswordResetLinkAsync(user, "test@example.com", resetLink));

        // Assert
        logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    private static SmtpEmailConfiguration ConfigurationWith(
        Option<Authentication> authentication,
        Option<string> from = default) =>
        new("127.0.0.1", 1, new Ssl(false), authentication, None, from);

    [Fact]
    public void SenderAddress_ConfiguredFrom_IsPreferredOverCredentials()
    {
        var configuration = ConfigurationWith(
            Some((Authentication)new BasicAuthentication("login@example.com", "password")),
            Some("noreply@example.com"));

        Assert.Equal(Some("noreply@example.com"), configuration.SenderAddress);
    }

    [Fact]
    public void SenderAddress_WithoutFrom_UsesBasicAuthenticationUsername()
    {
        var configuration = ConfigurationWith(
            Some((Authentication)new BasicAuthentication("login@example.com", "password")));

        Assert.Equal(Some("login@example.com"), configuration.SenderAddress);
    }

    [Fact]
    public void SenderAddress_WithoutFromAndCredentials_IsNone()
    {
        Assert.Equal(None, ConfigurationWith(new NoAuthentication()).SenderAddress);
        Assert.Equal(None, ConfigurationWith(None).SenderAddress);
    }
}
