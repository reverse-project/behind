using Behind.MarkdownViewer.Common;

namespace Behind.Tests.MarkdownViewer;

public sealed class MarkdownWorkspaceTests : IDisposable
{
    private readonly MarkdownSourceTestBed _bed = new MarkdownSourceTestBed().With("README.md", "# Home");

    public void Dispose() => _bed.Dispose();

    private static IReadOnlyList<string> Errors<T>(Validation<T> validation) =>
        validation.Match(errors => errors.Select(error => error.ToString()).ToList(), _ => []);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForViewing_WithoutASourceDirectory_IsInvalid(string? source)
    {
        var configuration = _bed.Configuration with { SourceDirectory = source };

        Assert.Equal([MarkdownWorkspaceErrors.SourceNotConfigured], Errors(configuration.ForViewing(_bed.Root)));
    }

    [Fact]
    public void ForViewing_WithAMissingSourceDirectory_IsInvalid()
    {
        var configuration = _bed.Configuration with { SourceDirectory = Path.Combine(_bed.Root, "missing") };

        Assert.Equal([MarkdownWorkspaceErrors.SourceMissing], Errors(configuration.ForViewing(_bed.Root)));
    }

    [Fact]
    public void ForViewing_ResolvesRelativeDirectoriesAgainstTheRoot()
    {
        var configuration = _bed.Configuration with { SourceDirectory = "docs", OutputDirectory = "site" };

        var workspace = configuration.ForViewing(_bed.Root).GetOrElse(() => throw new InvalidOperationException());

        Assert.Equal(_bed.Source, workspace.Source.FullName);
        Assert.Equal(Some(_bed.Output), workspace.Output.Map(output => output.FullName));
    }

    [Fact]
    public void ForViewing_DoesNotNeedAnOutputDirectory()
    {
        var configuration = _bed.Configuration with { OutputDirectory = null };

        var workspace = configuration.ForViewing(_bed.Root).GetOrElse(() => throw new InvalidOperationException());

        Assert.Equal(None, workspace.Output);
    }

    [Fact]
    public void ForViewing_FallsBackToTheDefaultTitleAndDocument()
    {
        var configuration = _bed.Configuration with { Title = " ", DefaultDocument = null };

        var workspace = configuration.ForViewing(_bed.Root).GetOrElse(() => throw new InvalidOperationException());

        Assert.Equal(MarkdownViewerConfiguration.DefaultTitle, workspace.Title);
        Assert.Equal(MarkdownViewerConfiguration.DefaultDocumentName, workspace.DefaultDocument);
    }

    [Fact]
    public void ForViewing_NormalizesTheDefaultDocument()
    {
        var configuration = _bed.Configuration with { Title = " Guide ", DefaultDocument = "\\guide\\index.md" };

        var workspace = configuration.ForViewing(_bed.Root).GetOrElse(() => throw new InvalidOperationException());

        Assert.Equal("Guide", workspace.Title);
        Assert.Equal("guide/index.md", workspace.DefaultDocument);
    }

    [Fact]
    public void ForGeneration_WithoutAnOutputDirectory_IsInvalid()
    {
        var configuration = _bed.Configuration with { OutputDirectory = null };

        Assert.Equal([MarkdownWorkspaceErrors.OutputNotConfigured], Errors(configuration.ForGeneration(_bed.Root)));
    }

    [Fact]
    public void ForGeneration_WithoutASourceDirectory_ReportsTheSource()
    {
        var configuration = _bed.Configuration with { SourceDirectory = null };

        Assert.Equal([MarkdownWorkspaceErrors.SourceNotConfigured], Errors(configuration.ForGeneration(_bed.Root)));
    }

    [Theory]
    [InlineData("docs")]
    [InlineData("docs/site")]
    [InlineData(".")]
    public void ForGeneration_WhenTheDirectoriesContainEachOther_IsInvalid(string output)
    {
        var configuration = _bed.Configuration with { OutputDirectory = output };

        Assert.Equal([MarkdownWorkspaceErrors.OutputOverlapsSource], Errors(configuration.ForGeneration(_bed.Root)));
    }

    [Fact]
    public void ForGeneration_WithSiblingDirectoriesSharingAPrefix_IsValid()
    {
        var configuration = _bed.Configuration with { OutputDirectory = "docs-site" };

        var generation = configuration.ForGeneration(_bed.Root).GetOrElse(() => throw new InvalidOperationException());

        Assert.Equal(Path.Combine(_bed.Root, "docs-site"), generation.Output.FullName);
        Assert.Equal(_bed.Source, generation.Workspace.Source.FullName);
    }

    [Fact]
    public void IsWithin_IsTrueForTheDirectoryItselfAndItsChildren()
    {
        var parent = new DirectoryInfo(_bed.Source);

        Assert.True(parent.IsWithin(parent));
        Assert.True(new DirectoryInfo(Path.Combine(_bed.Source, "a", "b")).IsWithin(parent));
        Assert.False(new DirectoryInfo(_bed.Source + "-site").IsWithin(parent));
        Assert.False(new DirectoryInfo(_bed.Root).IsWithin(parent));
    }
}
