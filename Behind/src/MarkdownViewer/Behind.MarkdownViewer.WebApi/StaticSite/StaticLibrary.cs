using Behind.MarkdownViewer.Common;
using Behind.MarkdownViewer.Domain;

namespace Behind.MarkdownViewer.WebApi.StaticSite;

public sealed record StaticLibrary(
    MarkdownWorkspace Workspace,
    IReadOnlyList<MarkdownPage> Pages,
    IReadOnlyList<MarkdownSection> Sections)
{
    public string Prefix => Workspace.LocaleCode is { } code ? code + "/" : string.Empty;
}
