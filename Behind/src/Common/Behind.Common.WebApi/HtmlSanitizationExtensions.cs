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

    private static readonly string[] Schemes = ["http", "https", "mailto"];

    private static readonly string[] Classes =
    [
        "ql-align-center", "ql-align-right", "ql-align-justify", "ql-direction-rtl", "ql-syntax",
        "ql-image-left", "ql-image-center", "ql-image-right",
        ..Enumerable.Range(1, 8).Select(level => $"ql-indent-{level}")
    ];

    extension(string html)
    {
        public string SanitizeRichText() => CreateSanitizer().Sanitize(html);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedClasses.Clear();
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedTags.UnionWith(Tags);
        sanitizer.AllowedAttributes.UnionWith(Attributes);
        sanitizer.AllowedSchemes.UnionWith(Schemes);
        sanitizer.AllowedClasses.UnionWith(Classes);

        return sanitizer;
    }
}
