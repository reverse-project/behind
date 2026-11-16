namespace Behind.MarkdownViewer.Domain;

public sealed record MarkdownPage(string Path, string Title, string Html, string? Locale = null);
