using Behind.Common.WebApi.Infrastructure.Localization;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public static class UserSettingExtensions
{
    extension(string key)
    {
        public async Task<bool> AcceptsAsync(
            string value,
            LocaleCatalog locales,
            CancellationToken cancellationToken = default) =>
            key != UserSettingKeys.Locale
            || value == string.Empty
            || await locales.ContainsAsync(value, cancellationToken);
    }
}
