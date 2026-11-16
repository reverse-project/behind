namespace Behind.MarkdownViewer.WebApi.Markdown;

public sealed record MarkdownLinkTarget(string Original, string Resolved, string Suffix, string? Locale = null)
{
    public bool IsDocument => Resolved.EndsWith(MarkdownFileExtensions.Markdown, StringComparison.OrdinalIgnoreCase);
}
