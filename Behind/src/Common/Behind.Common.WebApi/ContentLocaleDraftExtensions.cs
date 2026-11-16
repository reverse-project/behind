using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi.Infrastructure.Localization;

namespace Behind.Common.WebApi;

public static class ContentLocaleDraftExtensions
{
    extension(ContentLocaleDraft draft)
    {
        public async Task<bool> SelectContentLocale(
            IContentLocaleStore? context, LocaleCatalog? locales, CancellationToken cancellationToken)
        {
            if (!draft.ContentLocaleSpecified || context is null || locales is null)
            {
                return true;
            }
            var locale = await locales.NormalizeContentLocale(draft.ContentLocale, cancellationToken);
            if (draft.ContentLocale is not null && locale is null)
            {
                return false;
            }
            context.ContentLocaleSpecified = true;
            context.SelectedContentLocale = locale;
            return true;
        }
    }

    extension(LocaleCatalog locales)
    {
        public async Task<string?> NormalizeContentLocale(string? locale, CancellationToken cancellationToken) =>
            locale is null ? null : (await locales.ListAsync(cancellationToken))
                .FirstOrDefault(item => string.Equals(item.Code, locale, StringComparison.OrdinalIgnoreCase))?.Code;
    }
}
