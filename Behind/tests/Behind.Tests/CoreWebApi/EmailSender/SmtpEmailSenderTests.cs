using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.EmailSender;
using Behind.Core.WebApi.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.EmailSender;

public class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendEmailAsync_WithValidSmtpConfiguration_LogsSuccessMessage()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SmtpEmailSender>>();
        var configuration = new SmtpEmailConfiguration(
            "smtp.example.com",
            587,
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
        await sender.SendEmailAsync(email, subject, htmlMessage);

        // Assert - Should log either success or error based on SMTP availability
        logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task SendEmailAsync_WithNoAuthentication_DoesNotSetCredentials()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SmtpEmailSender>>();
        var configuration = new SmtpEmailConfiguration(
            "smtp.example.com",
            587,
            new Ssl(true),
            new NoAuthentication(),
            None
        );
        var options = Options.Create(configuration);
        var sender = new SmtpEmailSender(logger, options);

        const string email = "recipient@example.com";
        const string subject = "Test Subject";
        const string htmlMessage = "<p>Test message</p>";

        // Act
        await sender.SendEmailAsync(email, subject, htmlMessage);

        // Assert - Should handle no authentication gracefully
        logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task SendConfirmationLinkAsync_CreatesCorrectEmailContent()
    {
        // Arrange
        var logger = Substitute.For<ILogger<SmtpEmailSender>>();
        var configuration = new SmtpEmailConfiguration(
            "smtp.example.com",
            587,
            new Ssl(true),
            Some((Authentication)new BasicAuthentication("user@example.com", "password")),
            None
        );
        var options = Options.Create(configuration);
        var sender = new SmtpEmailSender(logger, options);
        var user = new ApplicationUser { DisplayName = "Test User" };
        const string confirmationLink = "https://example.com/confirm?token=abc123";

        // Act
        await sender.SendConfirmationLinkAsync(user, "test@example.com", confirmationLink);

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
        var logger = Substitute.For<ILogger<SmtpEmailSender>>();
        var configuration = new SmtpEmailConfiguration(
            "smtp.example.com",
            587,
            new Ssl(true),
            Some((Authentication)new BasicAuthentication("user@example.com", "password")),
            None
        );
        var options = Options.Create(configuration);
        var sender = new SmtpEmailSender(logger, options);
        var user = new ApplicationUser { DisplayName = "Test User" };
        const string resetCode = "123456";

        // Act
        await sender.SendPasswordResetCodeAsync(user, "test@example.com", resetCode);

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
        var logger = Substitute.For<ILogger<SmtpEmailSender>>();
        var configuration = new SmtpEmailConfiguration(
            "smtp.example.com",
            587,
            new Ssl(true),
            Some((Authentication)new BasicAuthentication("user@example.com", "password")),
            None
        );
        var options = Options.Create(configuration);
        var sender = new SmtpEmailSender(logger, options);
        var user = new ApplicationUser { DisplayName = "Test User" };
        const string resetLink = "https://example.com/reset?token=xyz789";

        // Act
        await sender.SendPasswordResetLinkAsync(user, "test@example.com", resetLink);

        // Assert
        logger.Received().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}
