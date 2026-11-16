using System.Text.Json.Nodes;
using Behind.Common.WebApi.Infrastructure.Localization;

namespace Behind.Core.WebApi.Settings;

public static class SettingsLocaleExtensions
{
    public const string DefaultLocalePath = "Frontend:Site:DefaultLocale";

    extension(IReadOnlyList<LocaleInfo> locales)
    {
        public IReadOnlyList<SettingsOption>? OptionsFor(string file, string path) =>
            IsDefaultLocale(file, path)
                ? [..locales.Select(locale => new SettingsOption(locale.Code, locale.Name))]
                : null;
    }

    extension(IReadOnlyDictionary<string, JsonNode?> planned)
    {
        public async Task<bool> HasKnownLocale(
            string file,
            LocaleCatalog locales,
            CancellationToken cancellationToken = default)
        {
            if (!IsDefaultLocale(file, DefaultLocalePath) || !planned.TryGetValue(DefaultLocalePath, out var node))
            {
                return true;
            }

            return node is JsonValue value
                && value.TryGetValue<string>(out var code)
                && await locales.ContainsAsync(code, cancellationToken);
        }
    }

    private static bool IsDefaultLocale(string file, string path) =>
        string.Equals(file, ConfigurationFileCatalog.FrontendFile, StringComparison.OrdinalIgnoreCase)
        && path == DefaultLocalePath;
}
