using System.Net;
using System.Net.Mail;
using Behind.Common.Logging;
using Behind.Common.Templating;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.EmailSender;

public abstract class EmailSender : IEmailSender, IEmailSender<ApplicationUser>, IMagicLinkEmailSender
{
    public virtual Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var template = DefaultEmailTemplates.ConfirmationEmail
            .AddParameter(new Parameter("Email", email))
            .AddParameter(new Parameter("ConfirmationLink", confirmationLink))
            .Render();

        return SendEmailAsync(email, "Confirm your email", template);
    }

    public virtual Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var template = DefaultEmailTemplates.PasswordResetCodeEmail
            .AddParameter(new Parameter("Email", email))
            .AddParameter(new Parameter("ResetCode", resetCode))
            .Render();

        return SendEmailAsync(email, "Password reset code", template);
    }

    public virtual Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var template = DefaultEmailTemplates.PasswordResetLinkEmail
            .AddParameter(new Parameter("Email", email))
            .AddParameter(new Parameter("ResetLink", resetLink))
            .Render();

        return SendEmailAsync(email, "Password reset link", template);
    }

    public virtual Task SendMagicLinkAsync(ApplicationUser user, string email, string magicLink)
    {
        var template = DefaultEmailTemplates.MagicLinkEmail
            .AddParameter(new Parameter("Email", email))
            .AddParameter(new Parameter("MagicLink", magicLink))
            .Render();

        return SendEmailAsync(email, "Your sign in link", template);
    }

    public abstract Task SendEmailAsync(string email, string subject, string htmlMessage);
}

public sealed class LoggerEmailSender : EmailSender
{
    private readonly ILogger<LoggerEmailSender> _logger;
    private readonly IHostEnvironment _environment;

    public LoggerEmailSender(ILogger<LoggerEmailSender> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public override Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogInformation("Sending email to {Email} with subject {Subject}", () => [email, subject]);

        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("Email body for {Email}: {Body}", () => [email, htmlMessage]);
        }

        return Task.CompletedTask;
    }
}

public sealed class SmtpEmailSender : EmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly IOptions<SmtpEmailConfiguration> _emailOptions;

    public SmtpEmailSender(ILogger<SmtpEmailSender> logger, IOptions<SmtpEmailConfiguration> emailOptions)
    {
        _logger = logger;
        _emailOptions = emailOptions;
    }

    public override Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var template = _emailOptions.Value.Templates.Match(
                none: () => DefaultEmailTemplates.ConfirmationEmail,
                some: t => (SimpleTemplate)t.GetOrDefault("ConfirmationEmail", DefaultEmailTemplates.ConfirmationEmail)
            )
            .AddParameter(new Parameter("Email", email))
            .AddParameter(new Parameter("ConfirmationLink", confirmationLink))
            .Render();

        return SendEmailAsync(email, "Confirm your email", template);
    }

    public override Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var template = _emailOptions.Value.Templates.Match(
                none: () => DefaultEmailTemplates.PasswordResetCodeEmail,
                some: t => (SimpleTemplate)t.GetOrDefault("PasswordResetCodeEmail", DefaultEmailTemplates.PasswordResetCodeEmail)
            )
            .AddParameter(new Parameter("Email", email))
            .AddParameter(new Parameter("ResetCode", resetCode))
            .Render();

        return SendEmailAsync(email, "Password reset code", template);
    }

    public override Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var template = _emailOptions.Value.Templates.Match(
                none: () => DefaultEmailTemplates.PasswordResetLinkEmail,
                some: t => (SimpleTemplate)t.GetOrDefault("PasswordResetLinkEmail", DefaultEmailTemplates.PasswordResetLinkEmail)
            )
            .AddParameter(new Parameter("Email", email))
            .AddParameter(new Parameter("ResetLink", resetLink))
            .Render();

        return SendEmailAsync(email, "Password reset link", template);
    }

    public override Task SendMagicLinkAsync(ApplicationUser user, string email, string magicLink)
    {
        var template = _emailOptions.Value.Templates.Match(
                none: () => DefaultEmailTemplates.MagicLinkEmail,
                some: t => (SimpleTemplate)t.GetOrDefault("MagicLinkEmail", DefaultEmailTemplates.MagicLinkEmail)
            )
            .AddParameter(new Parameter("Email", email))
            .AddParameter(new Parameter("MagicLink", magicLink))
            .Render();

        return SendEmailAsync(email, "Your sign in link", template);
    }

    public override async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        using var client = new SmtpClient(_emailOptions.Value.SmtpServer, _emailOptions.Value.Port);
        
        var authentication = _emailOptions.Value.Authentication.Match(
            none: () => new NoAuthentication(),
            auth => auth
        );

        if (authentication is BasicAuthentication b)
        {
            client.Credentials = new NetworkCredential(b.Username, b.Password);
        }

        client.EnableSsl = _emailOptions.Value.Ssl.Enabled;

        var sender = _emailOptions.Value.SenderAddress.Match(
            () => throw new InvalidOperationException(
                "The SMTP sender address is not configured. Set Email:From or use basic authentication with the mailbox address as the username."),
            address => address);

        var mailMessage = new MailMessage
        {
            From = new MailAddress(sender),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        mailMessage.To.Add(email);

        _logger.LogDebug(
            "Sending email from {Sender} to {Email} with subject {Subject} through {SmtpServer}:{Port} (SSL: {SslEnabled})",
            () =>
            [
                sender,
                email,
                subject,
                _emailOptions.Value.SmtpServer,
                _emailOptions.Value.Port,
                _emailOptions.Value.Ssl.Enabled
            ]);

        await client.SendMailAsync(mailMessage);

        _logger.LogInformation("Email sent successfully to {Email} with subject {Subject}", () => [email, subject]);
    }
}