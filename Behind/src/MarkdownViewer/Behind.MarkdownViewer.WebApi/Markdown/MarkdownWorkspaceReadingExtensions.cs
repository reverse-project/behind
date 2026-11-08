using Behind.Common;
using Behind.MarkdownViewer.Common;
using Behind.MarkdownViewer.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.MarkdownViewer.WebApi.Markdown;

public static class MarkdownWorkspaceReadingExtensions
{
    extension(MarkdownWorkspace workspace)
    {
        public async Task<IReadOnlyList<MarkdownSourceDocument>> ReadDocuments(CancellationToken cancellationToken)
        {
            var documents = await Task.WhenAll(workspace.Source.MarkdownFiles.Select(async file =>
                new MarkdownSourceDocument(
                    workspace.Source.PathOf(file),
                    await File.ReadAllTextAsync(file.FullName, cancellationToken))));

            return [..documents.OrderBy(document => document.Path, StringComparer.OrdinalIgnoreCase)];
        }

        public async Task<MarkdownNavigation> ListNavigation(CancellationToken cancellationToken) =>
            (await workspace.ReadDocuments(cancellationToken)).ToNavigation(workspace.DefaultDocument);

        public Task<Option<MarkdownPage>> ReadPage(
            string path,
            IReadOnlyList<string> locales,
            CancellationToken cancellationToken) =>
            workspace.Source.FindMarkdown(path).Match(
                () => Task.FromResult<Option<MarkdownPage>>(None),
                async file =>
                {
                    var markdown = await File.ReadAllTextAsync(file.FullName, cancellationToken);

                    return Some(markdown.Render(path, MarkdownLinks.ViewerFor(workspace.LocaleCode, locales)) with
                    {
                        Locale = workspace.LocaleCode
                    });
                });

        public async Task<Option<MarkdownPage>> ReadAnyPage(
            string path,
            string? locale,
            string? defaultLocale,
            CancellationToken cancellationToken)
        {
            var shared = await workspace.ReadSharedPage(path, cancellationToken);

            return shared != None
                ? shared
                : await workspace.Candidates(locale, defaultLocale).ReadPage(path, workspace.Locales, cancellationToken);
        }

        public Task<Option<MarkdownPage>> ReadSharedPage(string path, CancellationToken cancellationToken) =>
            path.StartsWith(MarkdownLinks.SharedPrefix, StringComparison.Ordinal)
                ? workspace.Source.FindRootMarkdown(path[MarkdownLinks.SharedPrefix.Length..]).Match(
                    () => Task.FromResult<Option<MarkdownPage>>(None),
                    async file =>
                    {
                        var markdown = await File.ReadAllTextAsync(file.FullName, cancellationToken);

                        return Some(markdown.Render(path, MarkdownLinks.ViewerFor(null, workspace.Locales)));
                    })
                : Task.FromResult<Option<MarkdownPage>>(None);
    }

    extension(IReadOnlyList<MarkdownWorkspace> candidates)
    {
        public async Task<Option<MarkdownPage>> ReadPage(
            string path,
            IReadOnlyList<string> locales,
            CancellationToken cancellationToken)
        {
            foreach (var candidate in candidates)
            {
                var page = await candidate.ReadPage(path, locales, cancellationToken);

                if (page != None)
                {
                    return page;
                }
            }

            return None;
        }

        public Option<FileInfo> FindAsset(string path) =>
            candidates.SelectMany(candidate => candidate.Source.FindAsset(path).AsEnumerable()).Find(_ => true);
    }
}
