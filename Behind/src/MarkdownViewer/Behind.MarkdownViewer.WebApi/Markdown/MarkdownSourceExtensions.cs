using Behind.Common;
using Behind.MarkdownViewer.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.MarkdownViewer.WebApi.Markdown;

public static class MarkdownSourceExtensions
{
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
    };

    private static readonly EnumerationOptions RootOptions = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
    };

    extension(DirectoryInfo source)
    {
        public IEnumerable<FileInfo> RootMarkdownFiles =>
            source.EnumerateFiles("*", RootOptions).Where(file => file.IsMarkdown);

        public Option<FileInfo> FindRootMarkdown(string name) =>
            source.RootMarkdownFiles.Find(file => string.Equals(file.Name, name, StringComparison.Ordinal));

        public IEnumerable<FileInfo> MarkdownFiles =>
            source.EnumerateFiles("*", Options).Where(file => file.IsMarkdown);

        public IEnumerable<FileInfo> AssetFiles =>
            source.EnumerateFiles("*", Options).Where(file => file.AssetContentType != None);

        public string PathOf(FileInfo file) =>
            Path.GetRelativePath(source.FullName, file.FullName).Replace(Path.DirectorySeparatorChar, '/');

        public Option<FileInfo> FindMarkdown(string path) =>
            source.MarkdownFiles.Find(file => string.Equals(source.PathOf(file), path, StringComparison.Ordinal));

        public Option<FileInfo> FindAsset(string path) =>
            source.AssetFiles.Find(file => string.Equals(source.PathOf(file), path, StringComparison.Ordinal));
    }
}
