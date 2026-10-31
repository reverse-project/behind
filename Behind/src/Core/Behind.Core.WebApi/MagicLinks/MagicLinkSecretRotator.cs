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
            none: () => Task.FromResult(false),
            some: current => AdvanceAsync(current, cancellationToken));

    private async Task<bool> AdvanceAsync(MagicLinkSettings current, CancellationToken cancellationToken)
    {
        var advanced = current.Advance(timeProvider.GetUtcNow());

        if (advanced == current)
        {
            return false;
        }

        store.Replace(advanced);
        await PersistAsync(advanced, cancellationToken);

        return true;
    }

    private async Task PersistAsync(MagicLinkSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await persister.PersistAsync(settings, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to persist the rotated magic link secrets");
        }
    }
}
