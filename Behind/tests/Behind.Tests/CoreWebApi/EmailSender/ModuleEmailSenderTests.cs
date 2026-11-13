using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.EmailSender;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.EmailSender;

public class ModuleEmailSenderTests
{
    [Fact]
    public async Task LoggerProvider_LogsAndCompletesSuccessfully()
    {
        var logger = EnabledLogger.For<LoggerEmailSender>();
        var sender = new ModuleEmailSender(new LoggerEmailSender(logger, FakeHostEnvironment.Development));
        await sender.SendEmailAsync("recipient@example.com", "Contact", "<p>Message</p>", CancellationToken.None);
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("recipient@example.com") && o.ToString()!.Contains("Contact")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("<p>Message</p>")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task OtherProvider_ReceivesMessageAndPropagatesFailure()
    {
        var provider = new OtherEmailSender();
        var services = new ServiceCollection();
        services.AddSingleton<IEmailSender>(provider);
        services.AddTransient<IModuleEmailSender, ModuleEmailSender>();
        using var serviceProvider = services.BuildServiceProvider();
        var sender = serviceProvider.GetRequiredService<IModuleEmailSender>();

        await sender.SendEmailAsync("recipient@example.com", "Contact", "<p>Message</p>", CancellationToken.None);

        Assert.Equal(("recipient@example.com", "Contact", "<p>Message</p>"), provider.Message);
        provider.Failure = new HttpRequestException("Provider unavailable");
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sender.SendEmailAsync("recipient@example.com", "Contact", "<p>Message</p>", CancellationToken.None));
    }

    [Fact]
    public async Task OtherProvider_ObservesCancellation()
    {
        var provider = new OtherEmailSender { Pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var sender = new ModuleEmailSender(provider);
        using var cancellation = new CancellationTokenSource();
        var delivery = sender.SendEmailAsync("recipient@example.com", "Contact", "<p>Message</p>", cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delivery);
        provider.Pending.SetResult();
    }

    [Fact]
    public async Task ModuleAdapter_UsesCurrentConfiguredProviderForEachResolution()
    {
        var options = new MutableOptions { Value = new LoggerEmailConfiguration() };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(FakeHostEnvironment.Production);
        services.AddSingleton<IOptions<EmailConfiguration>>(options);
        services.AddTransient<IEmailSender>(EmailSenderExtensions.CurrentInstance);
        services.AddTransient<IModuleEmailSender, ModuleEmailSender>();
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IModuleEmailSender>().SendEmailAsync("to@example.com", "Subject", "Body", CancellationToken.None);

        options.Value = new SmtpEmailConfiguration("127.0.0.1", 1, new Ssl(false), new NoAuthentication(), None);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<IModuleEmailSender>().SendEmailAsync("to@example.com", "Subject", "Body", CancellationToken.None));
        Assert.Contains("sender address", error.Message);
    }

    [Fact]
    public async Task SmtpProvider_PropagatesDeliveryFailure()
    {
        var configuration = new SmtpEmailConfiguration(
            "127.0.0.1", 1, new Ssl(false), new NoAuthentication(), None, Some("sender@example.com"));
        var sender = Create(configuration);

        await Assert.ThrowsAsync<SmtpException>(() =>
            sender.SendEmailAsync("recipient@example.com", "Contact", "<p>Message</p>", CancellationToken.None));
    }

    [Fact]
    public async Task SmtpProvider_RequiresConfiguredSender()
    {
        var configuration = new SmtpEmailConfiguration(
            "127.0.0.1", 1, new Ssl(false), new NoAuthentication(), None);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create(configuration).SendEmailAsync(
                "recipient@example.com", "Contact", "<p>Message</p>", CancellationToken.None));

        Assert.Contains("sender address", error.Message);
    }

    [Fact]
    public async Task SmtpProvider_DeliversMessageToConfiguredRecipient()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = ReceiveMessage(listener, cancellation.Token);
        var configuration = new SmtpEmailConfiguration(
            "127.0.0.1", port, new Ssl(false), new NoAuthentication(), None, Some("sender@example.com"));

        await Create(configuration).SendEmailAsync(
            "inbox@example.com", "Contact submission", "<p>Name: Visitor</p>", cancellation.Token);
        var message = await received;

        Assert.Contains("RCPT TO:<inbox@example.com>", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Subject: Contact submission", message);
        Assert.Contains("<p>Name: Visitor</p>", message);
        Assert.Contains("Message-ID:", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelledDelivery_StopsBeforeSending()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Create(new LoggerEmailConfiguration()).SendEmailAsync(
                "recipient@example.com", "Contact", "<p>Message</p>", cancellation.Token));
    }

    [Fact]
    public void ModuleAdapter_IsResolvableThroughCommonInterface()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptions<EmailConfiguration>>(Options.Create<EmailConfiguration>(
            new LoggerEmailConfiguration()));
        services.AddSingleton<IHostEnvironment>(FakeHostEnvironment.Production);
        services.AddTransient<IEmailSender>(EmailSenderExtensions.CurrentInstance);
        services.AddTransient<IModuleEmailSender, ModuleEmailSender>();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<ModuleEmailSender>(provider.GetRequiredService<IModuleEmailSender>());
    }

    private static ModuleEmailSender Create(EmailConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(FakeHostEnvironment.Production);
        var provider = services.BuildServiceProvider();
        return new(configuration.GetInstance(provider));
    }

    private sealed class OtherEmailSender : IEmailSender
    {
        public (string Email, string Subject, string Body)? Message { get; private set; }
        public Exception? Failure { get; set; }
        public TaskCompletionSource? Pending { get; set; }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            Message = (email, subject, htmlMessage);
            return Failure is not null ? Task.FromException(Failure) : Pending?.Task ?? Task.CompletedTask;
        }

    }

    private sealed class MutableOptions : IOptions<EmailConfiguration>
    {
        public required EmailConfiguration Value { get; set; }
    }

    private static async Task<string> ReceiveMessage(TcpListener listener, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream);
        await using var writer = new StreamWriter(stream) { NewLine = "\r\n", AutoFlush = true };
        var message = new StringBuilder();
        await writer.WriteLineAsync("220 localhost SMTP ready".AsMemory(), cancellationToken);

        while (await reader.ReadLineAsync(cancellationToken) is { } command)
        {
            message.AppendLine(command);

            if (command.Equals("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 End with a dot".AsMemory(), cancellationToken);

                while (await reader.ReadLineAsync(cancellationToken) is { } line && line != ".")
                {
                    message.AppendLine(line);
                }

                await writer.WriteLineAsync("250 Accepted".AsMemory(), cancellationToken);
            }
            else if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 Goodbye".AsMemory(), cancellationToken);
                break;
            }
            else
            {
                await writer.WriteLineAsync("250 localhost".AsMemory(), cancellationToken);
            }
        }

        return message.ToString();
    }
}
