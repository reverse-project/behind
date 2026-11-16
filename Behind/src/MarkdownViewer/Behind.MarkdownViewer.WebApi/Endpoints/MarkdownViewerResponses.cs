using Behind.MarkdownViewer.Domain;

namespace Behind.MarkdownViewer.WebApi.Endpoints;

public sealed record MarkdownDocumentsResponse(
    bool Configured,
    string Title,
    string DefaultDocument,
    IReadOnlyList<string> Locales,
    string? Locale,
    string? Home,
    IReadOnlyList<MarkdownEntry> Entries,
    IReadOnlyList<MarkdownSection> Sections);

public sealed record MarkdownProblemsResponse(IReadOnlyList<string> Errors);
