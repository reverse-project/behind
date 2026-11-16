using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.EmailSender;
using Behind.Core.WebApi.Identity;
using Microsoft.Extensions.Logging;

namespace Behind.Tests.CoreWebApi.EmailSender;

public class LoggerEmailSenderTests
{
    [Fact]
    public async Task SendEmailAsync_LogsEmailInformation()
    {
        // Arrange
        var logger = EnabledLogger.For<LoggerEmailSender>();
        var sender = new LoggerEmailSender(logger, FakeHostEnvironment.Production);
        const string email = "test@example.com";
        const string subject = "Test Subject";
        const string htmlMessage = "<p>Test message</p>";

        // Act
        await sender.SendEmailAsync(email, subject, htmlMessage);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(email) && o.ToString()!.Contains(subject)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task SendEmailAsync_InDevelopment_LogsEmailBody()
    {
        var logger = EnabledLogger.For<LoggerEmailSender>();
        var sender = new LoggerEmailSender(logger, FakeHostEnvironment.Development);

        await sender.SendEmailAsync("test@example.com", "Subject", "<p>Body</p>");

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("<p>Body</p>")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task SendEmailAsync_OutsideDevelopment_DoesNotLogEmailBody(string environmentName)
    {
        var logger = EnabledLogger.For<LoggerEmailSender>();
        var sender = new LoggerEmailSender(logger, FakeHostEnvironment.Named(environmentName));

        await sender.SendEmailAsync("test@example.com", "Subject", "<p>Body</p>");

        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("<p>Body</p>")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task SendEmailAsync_CompletesSuccessfully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<LoggerEmailSender>>();
        var sender = new LoggerEmailSender(logger, FakeHostEnvironment.Production);

        // Act
        var task = sender.SendEmailAsync("test@example.com", "Subject", "<p>Message</p>");

        // Assert
        await task;
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task SendConfirmationLinkAsync_UsesCorrectTemplate()
    {
        // Arrange
        var logger = EnabledLogger.For<LoggerEmailSender>();
        var sender = new LoggerEmailSender(logger, FakeHostEnvironment.Production);
        var user = new ApplicationUser { DisplayName = "Test User" };
        const string confirmationLink = "https://example.com/confirm?token=abc123";

        // Act
        await sender.SendConfirmationLinkAsync(user, "test@example.com", confirmationLink);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Confirm your email")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task SendPasswordResetCodeAsync_UsesCorrectTemplate()
    {
        // Arrange
        var logger = EnabledLogger.For<LoggerEmailSender>();
        var sender = new LoggerEmailSender(logger, FakeHostEnvironment.Production);
        var user = new ApplicationUser { DisplayName = "Test User" };
        const string resetCode = "123456";

        // Act
        await sender.SendPasswordResetCodeAsync(user, "test@example.com", resetCode);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Password reset code")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task SendPasswordResetLinkAsync_UsesCorrectTemplate()
    {
        // Arrange
        var logger = EnabledLogger.For<LoggerEmailSender>();
        var sender = new LoggerEmailSender(logger, FakeHostEnvironment.Production);
        var user = new ApplicationUser { DisplayName = "Test User" };
        const string resetLink = "https://example.com/reset?token=xyz789";

        // Act
        await sender.SendPasswordResetLinkAsync(user, "test@example.com", resetLink);

        // Assert
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Password reset link")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}
