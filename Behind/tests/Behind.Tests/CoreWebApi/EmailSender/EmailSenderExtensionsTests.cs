using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.EmailSender;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.EmailSender;

public class EmailSenderExtensionsTests
{
    [Fact]
    public void GetInstance_WithLoggerEmailConfiguration_ReturnsLoggerEmailSender()
    {
        // Arrange
        var configuration = new LoggerEmailConfiguration();
        var logger = Substitute.For<ILogger<LoggerEmailSender>>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ILogger<LoggerEmailSender>)).Returns(logger);

        // Act
        var sender = configuration.GetInstance(serviceProvider);

        // Assert
        Assert.IsType<LoggerEmailSender>(sender);
        serviceProvider.Received(1).GetService(typeof(ILogger<LoggerEmailSender>));
    }

    [Fact]
    public void GetInstance_WithSmtpEmailConfiguration_ReturnsSmtpEmailSender()
    {
        // Arrange
        var smtpConfig = new SmtpEmailConfiguration(
            "smtp.example.com",
            587,
            new Ssl(true),
            Some((Authentication)new BasicAuthentication("user@example.com", "password")),
            None
        );
        var logger = Substitute.For<ILogger<SmtpEmailSender>>();
        var options = Options.Create(smtpConfig);
        
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ILogger<SmtpEmailSender>)).Returns(logger);
        serviceProvider.GetService(typeof(IOptions<SmtpEmailConfiguration>)).Returns(options);

        // Act
        var sender = smtpConfig.GetInstance(serviceProvider);

        // Assert
        Assert.IsType<SmtpEmailSender>(sender);
        serviceProvider.Received(1).GetService(typeof(ILogger<SmtpEmailSender>));
        serviceProvider.Received(1).GetService(typeof(IOptions<SmtpEmailConfiguration>));
    }

    [Fact]
    public void GetInstance_WithUnsupportedConfiguration_ThrowsNotSupportedException()
    {
        // Arrange
        var unsupportedConfig = new TestEmailConfiguration();
        var serviceProvider = Substitute.For<IServiceProvider>();

        // Act & Assert
        var exception = Assert.Throws<NotSupportedException>(() => unsupportedConfig.GetInstance(serviceProvider));
        Assert.Contains("Unsupported email configuration", exception.Message);
    }

    [Fact]
    public void GetInstance_LoggerEmailConfiguration_DoesNotRequireSmtpOptions()
    {
        // Arrange
        var configuration = new LoggerEmailConfiguration();
        var logger = Substitute.For<ILogger<LoggerEmailSender>>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        
        serviceProvider
            .GetService(typeof(ILogger<LoggerEmailSender>))
            .Returns(logger);

        // Act
        var sender = configuration.GetInstance(serviceProvider);

        // Assert
        Assert.IsType<LoggerEmailSender>(sender);
        // Verify that SMTP options were not requested
        serviceProvider.DidNotReceive().GetService(typeof(IOptions<SmtpEmailConfiguration>));
    }

    private sealed record TestEmailConfiguration() : EmailConfiguration("Test");
}
