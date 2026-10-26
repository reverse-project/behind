using System.Net;
using System.Net.Mail;
using Behind.Common.Templating;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.EmailSender;

public abstract class EmailSender : IEmailSender, IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var template = new SimpleTemplate("ConfirmationEmail", "<p>Please confirm your email ({{Email}}) by clicking <a href=\"{{ConfirmationLink}}\">here</a>.</p>")
            .AddParameter(new Parameter("Email", email))
            .AddParameter(new Parameter("ConfirmationLink", confirmationLink))
            .Render();

        return SendEmailAsync(email, "Confirm your email", template);
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var template = new SimpleTemplate("PasswordResetCodeEmail", "<p>Your password reset code for email ({{Email}}) is: <b>{{ResetCode}}</b></p>")
            .AddParameter(new Parameter("Email", email))
            .AddParameter(new Parameter("ResetCode", resetCode))
            .Render();

        return SendEmailAsync(email, "Password reset code", template);
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var template = new SimpleTemplate("PasswordResetLinkEmail", "<p>Please reset your password for email ({{Email}}) by clicking <a href=\"{{ResetLink}}\">here</a>.</p>")
            .AddParameter(new Parameter("Email", email))
            .AddParameter(new Parameter("ResetLink", resetLink))
            .Render();

        return SendEmailAsync(email, "Password reset link", template);
    }

    public abstract Task SendEmailAsync(string email, string subject, string htmlMessage);
}

public sealed class LoggerEmailSender : EmailSender
{
    private readonly ILogger<LoggerEmailSender> _logger;

    public LoggerEmailSender(ILogger<LoggerEmailSender> logger)
    {
        _logger = logger;
    }

    public override Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogInformation("Sending email to {email} with subject {subject}: {htmlMessage}", email, subject, htmlMessage);
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

        var mailMessage = new MailMessage
        {
            From = new MailAddress(email),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        mailMessage.To.Add(email);

        try
        {
            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully to {email} with subject {subject}", email, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {email}", email);
        }
    }
}