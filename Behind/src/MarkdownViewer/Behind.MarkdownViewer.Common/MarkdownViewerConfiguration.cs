namespace Behind.MarkdownViewer.Common;

public sealed record MarkdownViewerConfiguration
{
    public const string SectionName = "MarkdownViewer";
    public const string DefaultTitle = "Documentation";
    public const string DefaultDocumentName = "README.md";
    public const string SiteDefaultLocalePath = "Frontend:Site:DefaultLocale";

    public string? SourceDirectory { get; init; }

    public string? OutputDirectory { get; init; }

    public string? Title { get; init; }

    public string? DefaultDocument { get; init; }
}
