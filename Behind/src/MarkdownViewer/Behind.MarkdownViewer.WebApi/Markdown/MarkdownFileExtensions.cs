using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.MarkdownViewer.WebApi.Markdown;

public static class MarkdownFileExtensions
{
    public const string Markdown = ".md";
    public const string Html = ".html";

    private static readonly IReadOnlyDictionary<string, string> AssetContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".svg"] = "image/svg+xml",
            [".ico"] = "image/x-icon"
        };

    extension(FileInfo file)
    {
        public bool IsMarkdown => string.Equals(file.Extension, Markdown, StringComparison.OrdinalIgnoreCase);

        public Option<string> AssetContentType =>
            AssetContentTypes.TryGetValue(file.Extension, out var contentType) ? Some(contentType) : None;
    }

    extension(string path)
    {
        public string ToHtmlPath() =>
            string.Concat(path.AsSpan(0, path.Length - Markdown.Length), Html);
    }
}
