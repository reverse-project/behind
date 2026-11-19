using Behind.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.MagicLinks;

public sealed class MagicLinkSecretRotationService(
    MagicLinkSecretRotator rotator,
    TimeProvider timeProvider,
    ILogger<MagicLinkSecretRotationService> logger) : BackgroundService
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TryRotateAsync(stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TryRotateAsync(stoppingToken);
        }
    }

    private async Task TryRotateAsync(CancellationToken stoppingToken)
    {
        try
        {
            await rotator.RotateIfNeededAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is MagicLinkRotationUnavailableException or UnauthorizedAccessException)
        {
            stoppingToken.ThrowIfCancellationRequested();
            logger.LogWarning("Magic link rotation requires writable persistent configuration; retrying on the next check");
        }
        catch (ConfigurationSchemaException)
        {
            stoppingToken.ThrowIfCancellationRequested();
            logger.LogWarning("Magic link rotation rejected by configuration validation; retrying on the next check");
        }
        catch (IOException)
        {
            stoppingToken.ThrowIfCancellationRequested();
            logger.LogWarning("Magic link rotation persistence failed; retrying on the next check");
        }
    }
}
