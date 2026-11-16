using Behind.Common;
using Behind.MarkdownViewer.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.MarkdownViewer.WebApi.Markdown;

public static class MarkdownNavigationExtensions
{
    private static readonly string[] IndexNames = ["README.md", "index.md"];

    extension(IReadOnlyList<MarkdownSourceDocument> documents)
    {
        public MarkdownNavigation ToNavigation(string homePath)
        {
            var entries = documents.Select(document => new MarkdownEntry(document.Path, document.Markdown.TitleOf(document.Path))).ToList();
            var home = documents.Find(document => document.Path == homePath);
            var labels = home.Match(
                () => new Dictionary<string, string>(),
                document => document.Markdown.LinkLabels());

            return new MarkdownNavigation(
                home.Match<string?>(() => null, document => document.Path),
                entries,
                entries.ToSections(homePath, labels));
        }
    }

    extension(IReadOnlyList<MarkdownEntry> entries)
    {
        public IReadOnlyList<MarkdownSection> ToSections(string homePath, IReadOnlyDictionary<string, string> labels)
        {
            var others = entries.Where(entry => entry.Path != homePath).ToList();
            var general = others.Where(entry => !entry.Path.Contains('/')).ToList();
            var folders = others
                .Where(entry => entry.Path.Contains('/'))
                .GroupBy(entry => entry.Path.Split('/')[0])
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.ToSection(labels));

            return
            [
                ..general.Take(1).Select(_ => new MarkdownSection(string.Empty, null, general)),
                ..folders
            ];
        }
    }

    extension(IGrouping<string, MarkdownEntry> folder)
    {
        private MarkdownSection ToSection(IReadOnlyDictionary<string, string> labels)
        {
            var index = folder.Find(entry => IndexNames.Any(name =>
                string.Equals(entry.Path, $"{folder.Key}/{name}", StringComparison.OrdinalIgnoreCase)));

            return index.Match(
                () => new MarkdownSection(folder.Key.Humanize(), null, [..folder]),
                found => new MarkdownSection(
                    labels.TryGetValue(found.Path, out var label) ? label : found.Title,
                    found.Path,
                    [..folder.Where(entry => entry.Path != found.Path)]));
        }
    }

    extension(string name)
    {
        private string Humanize()
        {
            var words = name.Replace('-', ' ').Replace('_', ' ').Trim();

            return words.Length == 0 ? name : char.ToUpperInvariant(words[0]) + words[1..];
        }
    }
}
