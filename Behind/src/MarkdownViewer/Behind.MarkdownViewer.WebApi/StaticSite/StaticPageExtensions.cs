using System.Net;
using Behind.MarkdownViewer.Domain;
using Behind.MarkdownViewer.WebApi.Markdown;

namespace Behind.MarkdownViewer.WebApi.StaticSite;

public static class StaticPageExtensions
{
    private const int MaxIndentDepth = 3;

    extension(MarkdownPage page)
    {
        public string ToStaticHtml(
            string siteTitle,
            IReadOnlyList<MarkdownSection> sections,
            IReadOnlyList<StaticLanguage> languages)
        {
            var root = page.Path.RootPrefix();
            var siteRoot = page.Locale is null ? root : "../" + root;

            return $$"""
                <!DOCTYPE html>
                <html lang="{{page.Locale ?? "en"}}">
                <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{WebUtility.HtmlEncode(page.Title)}} - {{WebUtility.HtmlEncode(siteTitle)}}</title>
                <link rel="stylesheet" href="{{siteRoot}}{{StaticSiteExtensions.StyleFile}}">
                </head>
                <body>
                <header><a href="{{root}}{{StaticSiteExtensions.IndexFile}}">{{WebUtility.HtmlEncode(siteTitle)}}</a>{{languages.ToSwitcher(page, siteRoot)}}</header>
                <div class="layout">
                <nav><ul>
                {{string.Join(Environment.NewLine, sections.SelectMany(section => section.ToNavigationItems(page.Path, root)))}}
                </ul></nav>
                <main>
                {{page.Html}}
                </main>
                </div>
                </body>
                </html>
                """;
        }
    }

    extension(string path)
    {
        public string ToRedirectHtml() =>
            $$"""
                <!DOCTYPE html>
                <html>
                <head>
                <meta charset="utf-8">
                <meta http-equiv="refresh" content="0; url={{path.ToUrlPath()}}">
                <title>{{WebUtility.HtmlEncode(path)}}</title>
                </head>
                <body>
                <a href="{{path.ToUrlPath()}}">{{WebUtility.HtmlEncode(path)}}</a>
                </body>
                </html>
                """;

        private string RootPrefix() =>
            string.Concat(Enumerable.Repeat("../", path.Count(character => character == '/')));

        private string ToUrlPath() =>
            string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
    }

    extension(IReadOnlyList<string> locales)
    {
        public string ToLocaleRedirectHtml(string defaultLocale) =>
            $$"""
                <!DOCTYPE html>
                <html>
                <head>
                <meta charset="utf-8">
                <title>{{string.Join(" / ", locales)}}</title>
                <script>
                var locales = [{{string.Join(", ", locales.Select(locale => $"\"{locale}\""))}}];
                var preferred = (navigator.languages || [navigator.language]).map(function (language) {
                    return locales.find(function (locale) {
                        return locale.toLowerCase() === String(language).toLowerCase() || locale.toLowerCase().indexOf(String(language).toLowerCase().split("-")[0] + "-") === 0;
                    });
                }).find(Boolean) || "{{defaultLocale}}";
                location.replace(preferred + "/{{StaticSiteExtensions.IndexFile}}");
                </script>
                <noscript><meta http-equiv="refresh" content="0; url={{defaultLocale}}/{{StaticSiteExtensions.IndexFile}}"></noscript>
                </head>
                <body>
                {{string.Join(" ", locales.Select(locale => $"<a href=\"{locale}/{StaticSiteExtensions.IndexFile}\">{locale}</a>"))}}
                </body>
                </html>
                """;
    }

    extension(IReadOnlyList<StaticLanguage> languages)
    {
        private string ToSwitcher(MarkdownPage page, string siteRoot) =>
            languages.Count < 2
                ? string.Empty
                : $"""<nav class="languages" aria-label="Languages">{string.Join(" ", languages.Select(language => language.ToLink(page, siteRoot)))}</nav>""";
    }

    extension(StaticLanguage language)
    {
        private string ToLink(MarkdownPage page, string siteRoot)
        {
            if (language.Code == page.Locale)
            {
                return $"""<a lang="{language.Code}" aria-current="true">{WebUtility.HtmlEncode(language.Label)}</a>""";
            }

            var target = language.Paths.Contains(page.Path) ? page.Path.ToHtmlPath().ToUrlPath() : StaticSiteExtensions.IndexFile;

            return $"""<a lang="{language.Code}" hreflang="{language.Code}" href="{siteRoot}{language.Code}/{target}">{WebUtility.HtmlEncode(language.Label)}</a>""";
        }
    }

    extension(MarkdownSection section)
    {
        private IEnumerable<string> ToNavigationItems(string currentPath, string root)
        {
            var general = section.Title.Length == 0;
            var heading = general
                ? []
                : new[] { section.ToHeading(currentPath, root) };

            return heading.Concat(section.Entries.Select(entry => entry.ToNavigationItem(currentPath, root, general)));
        }

        private string ToHeading(string currentPath, string root) =>
            section.Path is null
                ? $"<li class=\"section\">{WebUtility.HtmlEncode(section.Title)}</li>"
                : $"<li class=\"section\"><a href=\"{root}{section.Path.ToHtmlPath().ToUrlPath()}\"{(section.Path == currentPath ? " aria-current=\"page\"" : string.Empty)}>{WebUtility.HtmlEncode(section.Title)}</a></li>";
    }

    extension(MarkdownEntry entry)
    {
        private string ToNavigationItem(string currentPath, string root, bool general)
        {
            var depth = Math.Min(Math.Max(entry.Path.Count(character => character == '/') - (general ? 0 : 1), 0), MaxIndentDepth);
            var level = general ? "top" : $"depth-{depth}";
            var current = entry.Path == currentPath ? " aria-current=\"page\"" : string.Empty;

            return $"<li class=\"{level}\"><a href=\"{root}{entry.Path.ToHtmlPath().ToUrlPath()}\"{current}>{WebUtility.HtmlEncode(entry.Title)}</a></li>";
        }
    }
}
