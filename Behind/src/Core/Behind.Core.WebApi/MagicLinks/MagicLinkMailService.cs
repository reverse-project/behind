using Behind.Common.Logging;
using Behind.Core.WebApi.EmailSender;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.MagicLinks;

public sealed class MagicLinkMailService(
    IMagicLinkMailQueue queue,
    IServiceScopeFactory scopes,
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
        logger.LogDebug("Sending magic link ({Function}) email to user {UserId}", () => [mail.Function, mail.User.Id]);

        try
        {
            await using var scope = scopes.CreateAsyncScope();

            await scope.ServiceProvider.GetRequiredService<IMagicLinkEmailSender>().Send(mail);

            logger.LogInformation("Magic link email sent to user {UserId}", () => [mail.User.Id]);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send a magic link email to user {UserId}", () => [mail.User.Id]);
        }
    }
}
