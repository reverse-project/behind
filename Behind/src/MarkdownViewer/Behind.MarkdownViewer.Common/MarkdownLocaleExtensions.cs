using System.Text.RegularExpressions;
using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.MarkdownViewer.Common;

public static class MarkdownLocaleExtensions
{
    private static readonly Regex LocaleFolderPattern = new(
        "^[a-z]{2,3}-[A-Z]{2}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly EnumerationOptions FolderOptions = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
    };

    extension(MarkdownWorkspace workspace)
    {
        public IReadOnlyList<string> Locales =>
            workspace.Locale == None
                ? [
                    ..workspace.Source
                        .EnumerateDirectories("*", FolderOptions)
                        .Select(folder => folder.Name)
                        .Where(name => LocaleFolderPattern.IsMatch(name))
                        .Order(StringComparer.Ordinal)
                ]
                : [];

        public bool IsLocalized => workspace.Locales.Count > 0;

        public string? LocaleCode => workspace.Locale.Match<string?>(() => null, code => code);

        public MarkdownWorkspace ForLocale(string code) =>
            workspace with
            {
                Source = new DirectoryInfo(Path.Combine(workspace.Source.FullName, code)),
                Locale = Some(code)
            };

        public IReadOnlyList<MarkdownWorkspace> Candidates(string? requested, string? defaultLocale)
        {
            var locales = workspace.Locales;

            return locales.Count == 0
                ? [workspace]
                : [
                    ..new[] { requested, defaultLocale, locales[0] }
                        .SelectMany(code => locales.Find(locale => string.Equals(locale, code, StringComparison.OrdinalIgnoreCase)).AsEnumerable())
                        .Distinct()
                        .Select(workspace.ForLocale)
                ];
        }
    }
}
