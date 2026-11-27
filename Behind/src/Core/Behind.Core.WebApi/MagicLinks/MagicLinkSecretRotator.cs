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

    public Task<bool> RotateNowAsync(
        CancellationToken cancellationToken = default,
        Func<Uuid, MagicLinkSettings>? initialize = null) =>
        RotateAsync(true, cancellationToken, initialize);

    private async Task<bool> RotateAsync(
        bool force, CancellationToken cancellationToken, Func<Uuid, MagicLinkSettings>? initialize = null)
    {
        await _rotationGate.WaitAsync(cancellationToken);

        try
        {
            return await store.Current.Match(
                none: async () =>
                {
                    if (force && initialize is not null)
                    {
                        var initial = initialize(Uuid.CreateVersion7(timeProvider.GetUtcNow()));
                        await persister.PersistAsync(initial, cancellationToken);
                        store.Replace(initial);
                        logger.LogInformation("Magic link secrets initialized");
                        return true;
                    }

                    logger.LogDebug("Magic link secret rotation skipped: magic links are not configured");
                    return false;
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
