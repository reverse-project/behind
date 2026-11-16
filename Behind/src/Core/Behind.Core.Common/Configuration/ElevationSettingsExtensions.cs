using Behind.Common;

namespace Behind.Core.Common.Configuration;

public static class ElevationSettingsExtensions
{
    extension(SecurityConfiguration configuration)
    {
        public bool IsElevationEnabled =>
            configuration.Elevation.Map(settings => settings.Enabled).GetOrElse(false);

        public Option<SecretKey> DeveloperElevationSecretKey =>
            configuration.Elevation.Match(
                none: () => (Option<SecretKey>)default,
                settings => settings.DeveloperSecretKey);
    }
}
