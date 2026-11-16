using Behind.Core.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.EmailSender;

public static class EmailSenderExtensions
{
    public static EmailSender CurrentInstance(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IOptions<EmailConfiguration>>().Value.GetInstance(serviceProvider);

    extension(EmailConfiguration configuration)
    {
        public EmailSender GetInstance(IServiceProvider serviceProvider) => configuration switch
        {
            LoggerEmailConfiguration _ => new LoggerEmailSender(
                serviceProvider.GetRequiredService<ILogger<LoggerEmailSender>>(),
                serviceProvider.GetRequiredService<IHostEnvironment>()
            ),
            SmtpEmailConfiguration smtp => new SmtpEmailSender(
                serviceProvider.GetRequiredService<ILogger<SmtpEmailSender>>(),
                Options.Create(smtp)
            ),
            _ => throw new NotSupportedException("Unsupported email configuration")
        };
    }
}