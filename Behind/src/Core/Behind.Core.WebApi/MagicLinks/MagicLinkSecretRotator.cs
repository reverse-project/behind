using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.MagicLinks;

public sealed class MagicLinkSecretRotator(
    IMagicLinkSettingsStore store,
    IMagicLinkSettingsPersister persister,
    TimeProvider timeProvider,
    ILogger<MagicLinkSecretRotator> logger)
{
    public Task<bool> RotateIfNeededAsync(CancellationToken cancellationToken = default) =>
        store.Current.Match(
            none: () =>
            {
                logger.LogDebug("Magic link secret rotation skipped: magic links are not configured");

                return Task.FromResult(false);
            },
            some: current => AdvanceAsync(current, cancellationToken));

    private async Task<bool> AdvanceAsync(MagicLinkSettings current, CancellationToken cancellationToken)
    {
        var advanced = current.Advance(timeProvider.GetUtcNow());

        if (advanced == current)
        {
            logger.LogDebug("Magic link secrets are current, no rotation needed");

            return false;
        }

        store.Replace(advanced);
        await PersistAsync(advanced, cancellationToken);

        logger.LogInformation("Magic link secrets rotated");

        return true;
    }

    private async Task PersistAsync(MagicLinkSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await persister.PersistAsync(settings, cancellationToken);

            logger.LogDebug("Persisted the rotated magic link secrets");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to persist the rotated magic link secrets");
        }
    }
}
