namespace Behind.MarkdownViewer.WebApi.Markdown;

public sealed record MarkdownLinks(
    Func<MarkdownLinkTarget, string> Document,
    Func<MarkdownLinkTarget, string> Asset,
    string? Locale,
    IReadOnlyList<string> Locales)
{
    public const string FilesRoute = "/markdownviewer/files/";
    public const string DocumentsRoute = "#/docs/";
    public const string SharedPrefix = "~/";

    public static MarkdownLinks Viewer { get; } = ViewerFor(null, []);

    public static MarkdownLinks Static { get; } = StaticFor(null);

    public static MarkdownLinks StaticFor(string? locale) => new(
        target => target.Original.ToHtmlPath() + target.Suffix,
        target => target.Original + target.Suffix,
        locale,
        []);

    public static MarkdownLinks ViewerFor(string? locale, IReadOnlyList<string> locales) => new(
        target => DocumentsRoute + Uri.EscapeDataString(target.Resolved) + LocaleQuery(target.Locale),
        target => FilesRoute
            + string.Join('/', target.Resolved.Split('/').Select(Uri.EscapeDataString))
            + LocaleQuery(target.Locale ?? locale),
        locale,
        locales);

    private static string LocaleQuery(string? locale) =>
        locale is null ? string.Empty : "?locale=" + Uri.EscapeDataString(locale);
}
