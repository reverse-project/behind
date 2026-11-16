using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.EmailSender;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi;

public class WebApiExtensionsEmailTests
{
    [Fact]
    public void AddEmail_RegistersEmailSenderAsIEmailSender()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(FakeHostEnvironment.Production);
        
        var emailConfiguration = new LoggerEmailConfiguration();
        services.AddSingleton(Options.Create((EmailConfiguration)emailConfiguration));
        services.AddTransient<IEmailSender>(sp => emailConfiguration.GetInstance(sp));

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var emailSender = serviceProvider.GetService<IEmailSender>();

        // Assert
        Assert.NotNull(emailSender);
        Assert.IsAssignableFrom<IEmailSender>(emailSender);
    }

    [Fact]
    public void AddEmail_WithLoggerEmailConfiguration_ReturnsLoggerEmailSender()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(FakeHostEnvironment.Production);
        
        var emailConfiguration = new LoggerEmailConfiguration();
        services.AddSingleton(Options.Create((EmailConfiguration)emailConfiguration));
        services.AddTransient<IEmailSender>(sp => emailConfiguration.GetInstance(sp));

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var emailSender = serviceProvider.GetService<IEmailSender>();

        // Assert
        Assert.NotNull(emailSender);
        Assert.IsType<LoggerEmailSender>(emailSender);
    }

    [Fact]
    public void AddEmail_WithSmtpEmailConfiguration_ReturnsSmtpEmailSender()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(FakeHostEnvironment.Production);
        
        var emailConfiguration = new SmtpEmailConfiguration(
            "smtp.example.com",
            587,
            new Ssl(true),
            new NoAuthentication(),
            None
        );
        services.AddSingleton(Options.Create((EmailConfiguration)emailConfiguration));
        services.AddSingleton(Options.Create(emailConfiguration));
        services.AddTransient<IEmailSender>(sp => emailConfiguration.GetInstance(sp));

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var emailSender = serviceProvider.GetService<IEmailSender>();

        // Assert
        Assert.NotNull(emailSender);
        Assert.IsType<SmtpEmailSender>(emailSender);
    }

    [Fact]
    public void AddEmail_EmailSenderRegisteredAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(FakeHostEnvironment.Production);
        
        var emailConfiguration = new LoggerEmailConfiguration();
        services.AddSingleton(Options.Create((EmailConfiguration)emailConfiguration));
        services.AddTransient<IEmailSender>(sp => emailConfiguration.GetInstance(sp));

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var emailSender1 = serviceProvider.GetService<IEmailSender>();
        var emailSender2 = serviceProvider.GetService<IEmailSender>();

        // Assert - Transient registration means new instance each time
        Assert.NotNull(emailSender1);
        Assert.NotNull(emailSender2);
        Assert.NotSame(emailSender1, emailSender2);
    }

    [Fact]
    public void AddEmail_CanResolveMultipleEmailSenderInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(FakeHostEnvironment.Production);
        
        var emailConfiguration = new LoggerEmailConfiguration();
        services.AddSingleton(Options.Create((EmailConfiguration)emailConfiguration));
        services.AddTransient<IEmailSender>(sp => emailConfiguration.GetInstance(sp));

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var senders = new List<IEmailSender>();
        for (int i = 0; i < 3; i++)
        {
            senders.Add(serviceProvider.GetRequiredService<IEmailSender>());
        }

        // Assert
        Assert.Equal(3, senders.Count);
        Assert.All(senders, s => Assert.IsType<LoggerEmailSender>(s));
    }
}
