using Behind.Common;
using Behind.Core.Common.Configuration;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.MagicLinks;

public interface IMagicLinkSettingsStore
{
    Option<MagicLinkSettings> Current { get; }

    void Replace(MagicLinkSettings settings);
}

public sealed class MagicLinkSettingsStore(Option<MagicLinkSettings> initial) : IMagicLinkSettingsStore
{
    private MagicLinkSettings? _current = initial.Match<MagicLinkSettings?>(() => null, settings => settings);

    public Option<MagicLinkSettings> Current =>
        Volatile.Read(ref _current) is { } settings ? Some(settings) : None;

    public void Replace(MagicLinkSettings settings) => Volatile.Write(ref _current, settings);
}
