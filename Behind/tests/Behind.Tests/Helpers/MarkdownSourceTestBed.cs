using Behind.MarkdownViewer.Common;

namespace Behind.Tests.Helpers;

public sealed class MarkdownSourceTestBed : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory().FullName;

    public string Source => Path.Combine(Root, "docs");

    public string Output => Path.Combine(Root, "site");

    public MarkdownViewerConfiguration Configuration => new()
    {
        SourceDirectory = Source,
        OutputDirectory = Output,
        Title = "Docs",
        DefaultDocument = "README.md"
    };

    public MarkdownWorkspace Workspace =>
        Configuration.ForViewing(Root).GetOrElse(() => throw new InvalidOperationException());

    public MarkdownGeneration Generation =>
        Configuration.ForGeneration(Root).GetOrElse(() => throw new InvalidOperationException());

    public MarkdownSourceTestBed With(string path, string content)
    {
        var target = Path.Combine(Source, path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, content);

        return this;
    }

    public MarkdownSourceTestBed WithBytes(string path, byte[] content)
    {
        var target = Path.Combine(Source, path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(target, content);

        return this;
    }

    public string ReadOutput(string path) => File.ReadAllText(Path.Combine(Output, path));

    public bool OutputExists(string path) => File.Exists(Path.Combine(Output, path));

    public void Dispose() => Directory.Delete(Root, true);
}
