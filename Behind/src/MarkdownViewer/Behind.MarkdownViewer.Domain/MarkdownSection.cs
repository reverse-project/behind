namespace Behind.MarkdownViewer.Domain;

public sealed record MarkdownSection(string Title, string? Path, IReadOnlyList<MarkdownEntry> Entries);
