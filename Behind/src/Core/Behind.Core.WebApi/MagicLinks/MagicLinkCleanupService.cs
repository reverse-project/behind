using Behind.Common.Logging;
using Behind.Core.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.MagicLinks;

public sealed class MagicLinkCleanupService(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    ILogger<MagicLinkCleanupService> logger) : BackgroundService
{
    public static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    public async Task<int> CleanAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();

        return await scope.ServiceProvider
            .GetRequiredService<IMagicLinkCommands>()
            .DeleteExpired(timeProvider.GetUtcNow(), cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval, timeProvider);

        do
        {
            await TryCleanAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TryCleanAsync(CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await CleanAsync(cancellationToken);

            logger.Log(
                deleted > 0 ? LogLevel.Information : LogLevel.Debug,
                "Deleted {DeletedCount} expired magic links",
                () => [deleted]);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to delete expired magic links");
        }
    }
}
