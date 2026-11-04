using Microsoft.Extensions.FileProviders;

namespace Behind.Common.WebApi.Infrastructure.Localization;

public sealed class LocaleCatalog(IFileProvider files)
{
    public const string Directory = "locales";
    public const string FileExtension = ".json";
    public const string Fallback = "en-US";

    public async Task<IReadOnlyList<LocaleInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var found = await Task.WhenAll(
            files.GetDirectoryContents(Directory).Select(file => file.ToLocaleAsync(cancellationToken)));

        return
        [
            ..found
                .SelectMany(locale => locale.AsEnumerable())
                .OrderBy(locale => locale.Name, StringComparer.OrdinalIgnoreCase)
        ];
    }

    public async Task<bool> ContainsAsync(string code, CancellationToken cancellationToken = default) =>
        (await ListAsync(cancellationToken)).Any(locale => locale.Code == code);
}
