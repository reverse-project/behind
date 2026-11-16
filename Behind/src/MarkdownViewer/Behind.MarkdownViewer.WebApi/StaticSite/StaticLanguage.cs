namespace Behind.MarkdownViewer.WebApi.StaticSite;

public sealed record StaticLanguage(string Code, string Label, IReadOnlySet<string> Paths);
