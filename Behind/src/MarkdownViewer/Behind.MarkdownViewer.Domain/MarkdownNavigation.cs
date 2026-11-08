namespace Behind.MarkdownViewer.Domain;

public sealed record MarkdownNavigation(
    string? Home,
    IReadOnlyList<MarkdownEntry> Entries,
    IReadOnlyList<MarkdownSection> Sections);
