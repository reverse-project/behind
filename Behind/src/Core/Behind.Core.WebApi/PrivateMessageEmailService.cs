using System.Data.Common;
using System.Net.Mail;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi;

public sealed class PrivateMessageEmailService(
    IServiceScopeFactory scopes, TimeProvider clock, ILogger<PrivateMessageEmailService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PrivateMessageEmailDispatcher>().Deliver(stoppingToken);
            }
            catch (Exception exception) when (exception is DbException or DbUpdateException or SmtpException or SocketException
                                              or IOException or HttpRequestException or InvalidOperationException)
            {
                logger.LogError(exception, "Private message email processing failed; interrupted sending is not automatically retried");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
