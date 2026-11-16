using Microsoft.Extensions.Hosting;

namespace Behind.Core.WebApi.MagicLinks;

public sealed class MagicLinkSecretRotationService(MagicLinkSecretRotator rotator, TimeProvider timeProvider)
    : BackgroundService
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await rotator.RotateIfNeededAsync(stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await rotator.RotateIfNeededAsync(stoppingToken);
        }
    }
}
