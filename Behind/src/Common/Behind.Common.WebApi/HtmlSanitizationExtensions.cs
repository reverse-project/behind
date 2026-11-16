using Ganss.Xss;

namespace Behind.Common.WebApi;

public static class HtmlSanitizationExtensions
{
    private static readonly string[] Tags =
    [
        "p", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6", "strong", "b", "em", "i", "u", "s", "strike", "sub", "sup",
        "ul", "ol", "li", "blockquote", "pre", "code", "a", "img", "span", "div"
    ];

    private static readonly string[] Attributes = ["href", "src", "alt", "title", "target", "rel", "class", "width"];

    private static readonly string[] MarkdownTags =
    [
        ..Tags,
        "del", "ins", "mark", "abbr", "kbd", "dl", "dt", "dd", "table", "thead", "tbody", "tfoot", "tr", "th", "td",
        "input", "figure", "figcaption", "section"
    ];

    private static readonly string[] MarkdownAttributes =
    [
        ..Attributes,
        "id", "style", "colspan", "rowspan", "align", "type", "checked", "disabled", "start"
    ];

    private static readonly string[] MarkdownCssProperties = ["text-align"];

    private static readonly string[] Schemes = ["http", "https", "mailto"];

    private static readonly string[] Classes =
    [
        "ql-align-center", "ql-align-right", "ql-align-justify", "ql-direction-rtl", "ql-syntax",
        "ql-image-left", "ql-image-center", "ql-image-right",
        ..Enumerable.Range(1, 8).Select(level => $"ql-indent-{level}")
    ];

    extension(string html)
    {
        public string SanitizeRichText() => CreateSanitizer(Tags, Attributes, Classes, []).Sanitize(html);

        public string SanitizeMarkdown() =>
            CreateSanitizer(MarkdownTags, MarkdownAttributes, [], MarkdownCssProperties).Sanitize(html);
    }

    private static HtmlSanitizer CreateSanitizer(
        IEnumerable<string> tags,
        IEnumerable<string> attributes,
        IEnumerable<string> classes,
        IEnumerable<string> cssProperties)
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedClasses.Clear();
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedTags.UnionWith(tags);
        sanitizer.AllowedAttributes.UnionWith(attributes);
        sanitizer.AllowedSchemes.UnionWith(Schemes);
        sanitizer.AllowedClasses.UnionWith(classes);
        sanitizer.AllowedCssProperties.UnionWith(cssProperties);

        return sanitizer;
    }
}
