using Behind.Common;

namespace Behind.MarkdownViewer.Common;

public sealed record MarkdownWorkspace(
    DirectoryInfo Source,
    Option<DirectoryInfo> Output,
    string Title,
    string DefaultDocument,
    Option<string> Locale = default);
