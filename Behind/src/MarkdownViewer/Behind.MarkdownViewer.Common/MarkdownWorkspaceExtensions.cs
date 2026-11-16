using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.MarkdownViewer.Common;

public static class MarkdownWorkspaceExtensions
{
    extension(MarkdownViewerConfiguration configuration)
    {
        public Validation<MarkdownWorkspace> ForViewing(string root) =>
            configuration.SourceDirectory.ResolveDirectory(root).Match(
                () => Invalid<MarkdownWorkspace>(Error(MarkdownWorkspaceErrors.SourceNotConfigured)),
                source => source.Exists
                    ? Valid(new MarkdownWorkspace(
                        source,
                        configuration.OutputDirectory.ResolveDirectory(root),
                        configuration.Title.OrElse(MarkdownViewerConfiguration.DefaultTitle),
                        configuration.DefaultDocument.OrElse(MarkdownViewerConfiguration.DefaultDocumentName)
                            .Replace('\\', '/')
                            .TrimStart('/')))
                    : Invalid<MarkdownWorkspace>(Error(MarkdownWorkspaceErrors.SourceMissing)));

        public Validation<MarkdownGeneration> ForGeneration(string root) =>
            configuration.ForViewing(root).Bind(workspace => workspace.Output.Match(
                () => Invalid<MarkdownGeneration>(Error(MarkdownWorkspaceErrors.OutputNotConfigured)),
                output => output.Overlaps(workspace.Source)
                    ? Invalid<MarkdownGeneration>(Error(MarkdownWorkspaceErrors.OutputOverlapsSource))
                    : Valid(new MarkdownGeneration(workspace, output))));
    }

    extension(DirectoryInfo directory)
    {
        public bool Overlaps(DirectoryInfo other) => directory.IsWithin(other) || other.IsWithin(directory);

        public bool IsWithin(DirectoryInfo parent)
        {
            var path = Path.TrimEndingDirectorySeparator(directory.FullName);
            var root = Path.TrimEndingDirectorySeparator(parent.FullName);

            return string.Equals(path, root, Comparison)
                || path.StartsWith(root + Path.DirectorySeparatorChar, Comparison);
        }
    }

    extension(string? value)
    {
        private Option<DirectoryInfo> ResolveDirectory(string root) =>
            string.IsNullOrWhiteSpace(value)
                ? None
                : Some(new DirectoryInfo(Path.GetFullPath(value.Trim(), root)));

        private string OrElse(string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static StringComparison Comparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
