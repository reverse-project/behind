using Behind.MarkdownViewer.Common;
using Behind.MarkdownViewer.Domain;
using Behind.MarkdownViewer.WebApi.Markdown;

namespace Behind.MarkdownViewer.WebApi.StaticSite;

public static class StaticSiteExtensions
{
    public const string ManifestFile = ".markdownviewer-generated";
    public const string StyleFile = "markdownviewer.css";
    public const string IndexFile = "index.html";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    extension(MarkdownGeneration generation)
    {
        public async Task<GeneratedSite> Generate(string? defaultLocale, CancellationToken cancellationToken)
        {
            await Gate.WaitAsync(cancellationToken);

            try
            {
                return await generation.Write(defaultLocale, cancellationToken);
            }
            finally
            {
                Gate.Release();
            }
        }

        private async Task<GeneratedSite> Write(string? defaultLocale, CancellationToken cancellationToken)
        {
            var (workspace, output) = (generation.Workspace, generation.Output);
            var libraries = await Task.WhenAll(workspace.Libraries.Select(library => library.Render(cancellationToken)));
            var shared = await workspace.RenderShared(cancellationToken);
            var languages = libraries.ToLanguages();
            var documents = libraries
                .SelectMany(library => library.ToStaticDocuments(languages, true))
                .Concat(shared.ToStaticDocuments([], false));
            var assets = libraries.SelectMany(library => library.ToStaticAssets()).ToList();
            var files = documents
                .Concat(assets)
                .Concat(workspace.ToRootFiles(defaultLocale))
                .ToList();

            output.Create();
            var removed = output.RemoveStale(files.Select(file => file.Path).ToHashSet(StringComparer.Ordinal));

            foreach (var file in files)
            {
                await output.Place(file, cancellationToken);
            }

            await output.RecordManifest(files.Select(file => file.Path), cancellationToken);

            return new GeneratedSite(output.FullName, libraries.Append(shared).Sum(library => library.Pages.Count), assets.Count, removed);
        }
    }

    extension(MarkdownWorkspace workspace)
    {
        private IReadOnlyList<MarkdownWorkspace> Libraries =>
            workspace.IsLocalized ? [..workspace.Locales.Select(workspace.ForLocale)] : [workspace];

        private async Task<StaticLibrary> Render(CancellationToken cancellationToken)
        {
            var documents = await workspace.ReadDocuments(cancellationToken);
            var pages = documents
                .Select(document => document.Markdown.Render(document.Path, MarkdownLinks.StaticFor(workspace.LocaleCode)) with
                {
                    Locale = workspace.LocaleCode
                })
                .ToList();

            return new StaticLibrary(workspace, pages, documents.ToNavigation(workspace.DefaultDocument).Sections);
        }

        private async Task<StaticLibrary> RenderShared(CancellationToken cancellationToken)
        {
            if (!workspace.IsLocalized)
            {
                return new StaticLibrary(workspace, [], []);
            }

            var documents = await Task.WhenAll(workspace.Source.RootMarkdownFiles.Select(async file =>
                new MarkdownSourceDocument(file.Name, await File.ReadAllTextAsync(file.FullName, cancellationToken))));
            var ordered = documents.OrderBy(document => document.Path, StringComparer.OrdinalIgnoreCase).ToList();
            var pages = ordered.Select(document => document.Markdown.Render(document.Path, MarkdownLinks.StaticFor(null))).ToList();

            return new StaticLibrary(workspace, pages, ordered.ToNavigation(string.Empty).Sections);
        }

        private IEnumerable<StaticFile> ToRootFiles(string? defaultLocale)
        {
            var locales = workspace.Locales;
            var landing = workspace.Candidates(null, defaultLocale)[0].LocaleCode;

            return landing is null
                ? [StaticFile.Text(StyleFile, StaticSiteStyle.Css)]
                : [
                    StaticFile.Text(StyleFile, StaticSiteStyle.Css),
                    StaticFile.Text(IndexFile, locales.ToLocaleRedirectHtml(landing))
                ];
        }
    }

    extension(StaticLibrary library)
    {
        private IEnumerable<StaticFile> ToStaticAssets() =>
            library.Workspace.Source.AssetFiles
                .Select(file => StaticFile.Copy(library.Prefix + library.Workspace.Source.PathOf(file), file));

        private IEnumerable<StaticFile> ToStaticDocuments(IReadOnlyList<StaticLanguage> languages, bool withIndex)
        {
            var title = library.Workspace.Title;
            var documents = library.Pages
                .Select(page => StaticFile.Text(
                    library.Prefix + page.Path.ToHtmlPath(),
                    page.ToStaticHtml(title, library.Sections, languages)))
                .ToList();

            return withIndex ? documents.Concat(library.IndexRedirect(documents)) : documents;
        }

        private IEnumerable<StaticFile> IndexRedirect(IReadOnlyList<StaticFile> documents)
        {
            var landing = library.Pages.FirstOrDefault(page => page.Path == library.Workspace.DefaultDocument)
                ?? library.Pages.FirstOrDefault();
            var index = library.Prefix + IndexFile;

            return landing is null || documents.Any(document => document.Path == index)
                ? []
                : [StaticFile.Text(index, landing.Path.ToHtmlPath().ToRedirectHtml())];
        }
    }

    extension(DirectoryInfo output)
    {
        private async Task Place(StaticFile file, CancellationToken cancellationToken)
        {
            var target = Path.Combine(output.FullName, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await file.WriteTo(target, cancellationToken);
        }

        private Task RecordManifest(IEnumerable<string> paths, CancellationToken cancellationToken) =>
            File.WriteAllLinesAsync(
                Path.Combine(output.FullName, ManifestFile),
                paths.Order(StringComparer.Ordinal),
                cancellationToken);

        private int RemoveStale(IReadOnlySet<string> current)
        {
            var manifest = new FileInfo(Path.Combine(output.FullName, ManifestFile));
            var stale = manifest.Exists
                ? File.ReadAllLines(manifest.FullName)
                    .Where(path => path.Length > 0 && !current.Contains(path))
                    .Select(path => new FileInfo(Path.GetFullPath(path, output.FullName)))
                    .Where(file => file.Exists && file.Directory is not null && file.Directory.IsWithin(output))
                    .ToList()
                : [];

            stale.ForEach(file =>
            {
                file.Delete();
                output.PruneEmptyDirectories(file.Directory);
            });

            return stale.Count;
        }

        private void PruneEmptyDirectories(DirectoryInfo? directory)
        {
            while (directory is not null
                   && directory.FullName != output.FullName
                   && directory.Exists
                   && !directory.EnumerateFileSystemInfos().Any())
            {
                directory.Delete();
                directory = directory.Parent;
            }
        }
    }
}
