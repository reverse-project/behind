using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.WebApi.Infrastructure.Localization;

public static class LocaleFileExtensions
{
    public const string NameProperty = "name";

    private static readonly Regex CodePattern = new(
        "^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    extension(IFileInfo file)
    {
        public async Task<Option<LocaleInfo>> ToLocaleAsync(CancellationToken cancellationToken = default)
        {
            if (!file.IsLocaleFile)
            {
                return None;
            }

            var json = await file.ReadTextAsync(cancellationToken);

            return json.ToLocale(Path.GetFileNameWithoutExtension(file.Name));
        }

        private bool IsLocaleFile =>
            !file.IsDirectory
            && string.Equals(Path.GetExtension(file.Name), LocaleCatalog.FileExtension, StringComparison.OrdinalIgnoreCase)
            && CodePattern.IsMatch(Path.GetFileNameWithoutExtension(file.Name));

        private async Task<string> ReadTextAsync(CancellationToken cancellationToken)
        {
            await using var stream = file.CreateReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            return await reader.ReadToEndAsync(cancellationToken);
        }
    }

    extension(string json)
    {
        private Option<LocaleInfo> ToLocale(string code) =>
            Try(() => JsonDocument.Parse(json)).Run().Match<Option<LocaleInfo>>(
                _ => None,
                document =>
                {
                    using (document)
                    {
                        return new LocaleInfo(code, document!.RootElement.NameOr(code));
                    }
                });
    }

    extension(JsonElement root)
    {
        private string NameOr(string fallback) =>
            root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(NameProperty, out var name)
            && name.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(name.GetString())
                ? name.GetString()!
                : fallback;
    }
}
