using Behind.MarkdownViewer.WebApi.Markdown;

namespace Behind.Tests.MarkdownViewer;

public sealed class MarkdownSourceExtensionsTests : IDisposable
{
    private readonly MarkdownSourceTestBed _bed = new MarkdownSourceTestBed()
        .With("README.md", "# Home")
        .With("guide/setup.MD", "# Setup")
        .With("notes.txt", "text")
        .With(".git/secret.md", "# Hidden")
        .With(".hidden.md", "# Hidden")
        .WithBytes("img/logo.png", [1, 2, 3])
        .WithBytes("img/run.exe", [1, 2, 3]);

    public void Dispose() => _bed.Dispose();

    private DirectoryInfo Source => new(_bed.Source);

    [Fact]
    public void MarkdownFiles_ListsTheVisibleMarkdownFilesRecursively()
    {
        var paths = Source.MarkdownFiles.Select(Source.PathOf).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["README.md", "guide/setup.MD"], paths);
    }

    [Fact]
    public void AssetFiles_ListsOnlyTheKnownAssetTypes()
    {
        Assert.Equal(["img/logo.png"], Source.AssetFiles.Select(Source.PathOf));
    }

    [Theory]
    [InlineData("README.md", true)]
    [InlineData("guide/setup.MD", true)]
    [InlineData("../secret.md", false)]
    [InlineData("guide/../README.md", false)]
    [InlineData(".git/secret.md", false)]
    [InlineData("notes.txt", false)]
    [InlineData("missing.md", false)]
    [InlineData("", false)]
    public void FindMarkdown_OnlyFindsListedDocuments(string path, bool found)
    {
        Assert.Equal(found, Source.FindMarkdown(path) != None);
    }

    [Theory]
    [InlineData("img/logo.png", true)]
    [InlineData("img/run.exe", false)]
    [InlineData("../logo.png", false)]
    [InlineData("README.md", false)]
    public void FindAsset_OnlyFindsListedAssets(string path, bool found)
    {
        Assert.Equal(found, Source.FindAsset(path) != None);
    }

    [Fact]
    public void MarkdownFiles_SkipsSymbolicLinks()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var outside = Directory.CreateTempSubdirectory().FullName;

        try
        {
            File.WriteAllText(Path.Combine(outside, "secret.md"), "# Secret");
            Directory.CreateSymbolicLink(Path.Combine(_bed.Source, "linked"), outside);
            File.CreateSymbolicLink(Path.Combine(_bed.Source, "link.md"), Path.Combine(outside, "secret.md"));

            Assert.DoesNotContain(Source.MarkdownFiles.Select(Source.PathOf), path => path.Contains("secret") || path.StartsWith("link"));
        }
        finally
        {
            Directory.Delete(outside, true);
        }
    }

    [Theory]
    [InlineData("a.png", "image/png")]
    [InlineData("a.JPG", "image/jpeg")]
    [InlineData("a.svg", "image/svg+xml")]
    public void AssetContentType_MapsTheExtension(string name, string contentType)
    {
        Assert.Equal(Some(contentType), new FileInfo(name).AssetContentType);
    }

    [Fact]
    public void AssetContentType_IsNoneForOtherFiles()
    {
        Assert.Equal(None, new FileInfo("a.exe").AssetContentType);
    }
}
