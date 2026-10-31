using Behind.Common.Configuration;
using Behind.Core.Common.Configuration;

namespace Behind.Core.WebApi.MagicLinks;

public interface IMagicLinkSettingsPersister
{
    Task PersistAsync(MagicLinkSettings settings, CancellationToken cancellationToken = default);
}

public sealed class NoOpMagicLinkSettingsPersister : IMagicLinkSettingsPersister
{
    public Task PersistAsync(MagicLinkSettings settings, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class JsonFileMagicLinkSettingsPersister(FileInfo file) : IMagicLinkSettingsPersister
{
    private const string Section = $"{SecurityConfiguration.SectionName}:{nameof(SecurityConfiguration.MagicLink)}";

    public Task PersistAsync(MagicLinkSettings settings, CancellationToken cancellationToken = default) =>
        file.SetJsonValuesAsync(
            new Dictionary<string, string?>
            {
                [$"{Section}:{nameof(MagicLinkSettings.ActualSecret)}"] = settings.ActualSecret.ToConfigurationValue(),
                [$"{Section}:{nameof(MagicLinkSettings.OldSecret)}"] =
                    settings.OldSecret.Match(() => (string?)null, secret => secret.ToConfigurationValue())
            },
            cancellationToken);
}
