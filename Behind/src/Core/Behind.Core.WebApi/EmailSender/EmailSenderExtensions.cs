using Behind.Core.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.EmailSender;

public static class EmailSenderExtensions
{
    extension(EmailConfiguration configuration)
    {
        public EmailSender GetInstance(IServiceProvider serviceProvider) => configuration switch
        {
            LoggerEmailConfiguration _ => new LoggerEmailSender(
                serviceProvider.GetRequiredService<ILogger<LoggerEmailSender>>()
            ),
            SmtpEmailConfiguration _ => new SmtpEmailSender(
                serviceProvider.GetRequiredService<ILogger<SmtpEmailSender>>(),
                serviceProvider.GetRequiredService<IOptions<SmtpEmailConfiguration>>()
            ),
            _ => throw new NotSupportedException("Unsupported email configuration")
        };
    }
}