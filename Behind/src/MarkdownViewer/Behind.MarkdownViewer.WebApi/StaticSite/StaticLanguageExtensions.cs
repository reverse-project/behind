using System.Globalization;
using Behind.Common;
using Behind.MarkdownViewer.Common;

namespace Behind.MarkdownViewer.WebApi.StaticSite;

public static class StaticLanguageExtensions
{
    extension(string code)
    {
        public string ToLanguageLabel() =>
            FunctionalExtensions.Try(() => CultureInfo.GetCultureInfo(code).NativeName).Run().Match(
                _ => code,
                name => string.IsNullOrWhiteSpace(name) ? code : name!);
    }

    extension(IEnumerable<StaticLibrary> libraries)
    {
        public IReadOnlyList<StaticLanguage> ToLanguages() =>
        [
            ..libraries
                .Where(library => library.Workspace.LocaleCode is not null && library.Pages.Count > 0)
                .Select(library => new StaticLanguage(
                    library.Workspace.LocaleCode!,
                    library.Workspace.LocaleCode!.ToLanguageLabel(),
                    library.Pages.Select(page => page.Path).ToHashSet(StringComparer.Ordinal)))
        ];
    }
}
