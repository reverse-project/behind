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

    public Task<bool> RotateNowAsync(CancellationToken cancellationToken = default) =>
        store.Current.Match(
            none: () => Task.FromResult(false),
            some: async current =>
            {
                var rotated = current.Rotate(timeProvider.GetUtcNow());

                await persister.PersistAsync(rotated, cancellationToken);
                store.Replace(rotated);

                logger.LogInformation("Magic link secrets rotated on request");

                return true;
            });

    private async Task<bool> AdvanceAsync(MagicLinkSettings current, CancellationToken cancellationToken)
    {
        var advanced = current.Advance(timeProvider.GetUtcNow());

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
