using Behind.Core.WebApi.EmailSender;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.MagicLinks;

public sealed class MagicLinkMailService(
    IMagicLinkMailQueue queue,
    IMagicLinkEmailSender emailSender,
    ILogger<MagicLinkMailService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var mail in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await SendAsync(mail);
        }
    }

    private async Task SendAsync(MagicLinkMail mail)
    {
        try
        {
            await emailSender.SendMagicLinkAsync(mail.User, mail.Email, mail.Link);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send a magic link email");
        }
    }
}
