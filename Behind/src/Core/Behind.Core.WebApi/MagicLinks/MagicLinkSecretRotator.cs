using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.MagicLinks;

public sealed class MagicLinkSecretRotator(
    IMagicLinkSettingsStore store,
    IMagicLinkSettingsPersister persister,
    TimeProvider timeProvider,
    ILogger<MagicLinkSecretRotator> logger)
{
    private readonly SemaphoreSlim _rotationGate = new(1, 1);

    public Task<bool> RotateIfNeededAsync(CancellationToken cancellationToken = default) =>
        RotateAsync(false, cancellationToken);

    public Task<bool> RotateNowAsync(CancellationToken cancellationToken = default) =>
        RotateAsync(true, cancellationToken);

    private async Task<bool> RotateAsync(bool force, CancellationToken cancellationToken)
    {
        await _rotationGate.WaitAsync(cancellationToken);

        try
        {
            return await store.Current.Match(
                none: () =>
                {
                    logger.LogDebug("Magic link secret rotation skipped: magic links are not configured");
                    return Task.FromResult(false);
                },
                some: current => AdvanceAsync(current, force, cancellationToken));
        }
        finally
        {
            _rotationGate.Release();
        }
    }

    private async Task<bool> AdvanceAsync(MagicLinkSettings current, bool force, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var advanced = force ? current.Rotate(now) : current.Advance(now);

        if (advanced == current)
        {
            logger.LogDebug("Magic link secrets are current, no rotation needed");
            return false;
        }

        await persister.PersistAsync(advanced, cancellationToken);
        store.Replace(advanced);
        logger.LogInformation("Magic link secrets rotated");
        return true;
    }
}
