using Behind.MarkdownViewer.Common;

namespace Behind.Tests.MarkdownViewer;

public sealed class MarkdownLocaleTests : IDisposable
{
    private readonly MarkdownSourceTestBed _bed = new MarkdownSourceTestBed()
        .With("README.md", "# Ignored root document")
        .With("en-US/README.md", "# Welcome")
        .With("hu-HU/README.md", "# Üdv")
        .With("de-DE/README.md", "# Willkommen")
        .With("api/README.md", "# Not a locale")
        .With("en/README.md", "# Not a locale either")
        .With("fr-fr/README.md", "# Wrong case");

    public void Dispose() => _bed.Dispose();

    private static IReadOnlyList<string?> Codes(IEnumerable<MarkdownWorkspace> candidates) =>
        [..candidates.Select(candidate => candidate.LocaleCode)];

    [Fact]
    public void Locales_ListsOnlyTheFoldersNamedAsLocaleCodes()
    {
        Assert.Equal(["de-DE", "en-US", "hu-HU"], _bed.Workspace.Locales);
        Assert.True(_bed.Workspace.IsLocalized);
    }

    [Fact]
    public void Locales_IsEmptyForAFlatDirectory()
    {
        using var flat = new MarkdownSourceTestBed().With("README.md", "# Home").With("api/README.md", "# Api");

        Assert.Empty(flat.Workspace.Locales);
        Assert.False(flat.Workspace.IsLocalized);
        Assert.Null(flat.Workspace.LocaleCode);
    }

    [Fact]
    public void Locales_AreNotNestedInsideALocale()
    {
        var german = _bed.Workspace.ForLocale("de-DE");
        Directory.CreateDirectory(Path.Combine(german.Source.FullName, "hu-HU"));

        Assert.Empty(german.Locales);
        Assert.Equal("de-DE", german.LocaleCode);
        Assert.Equal(Path.Combine(_bed.Source, "de-DE"), german.Source.FullName);
    }

    [Theory]
    [InlineData("hu-HU", "en-US", new[] { "hu-HU", "en-US", "de-DE" })]
    [InlineData("HU-hu", "en-US", new[] { "hu-HU", "en-US", "de-DE" })]
    [InlineData("de-DE", "de-DE", new[] { "de-DE" })]
    [InlineData("fr-FR", "en-US", new[] { "en-US", "de-DE" })]
    [InlineData(null, "hu-HU", new[] { "hu-HU", "de-DE" })]
    [InlineData("fr-FR", "xx-XX", new[] { "de-DE" })]
    [InlineData("../en-US", null, new[] { "de-DE" })]
    [InlineData("", "", new[] { "de-DE" })]
    public void Candidates_AreTheRequestedThenTheDefaultThenTheFirstLocale(string? requested, string? defaultLocale, string[] expected)
    {
        Assert.Equal(expected, Codes(_bed.Workspace.Candidates(requested, defaultLocale)));
    }

    [Fact]
    public void Candidates_OfAFlatDirectoryIsTheWorkspaceItself()
    {
        using var flat = new MarkdownSourceTestBed().With("README.md", "# Home");

        var candidates = flat.Workspace.Candidates("hu-HU", "en-US");

        Assert.Equal([flat.Source], candidates.Select(candidate => candidate.Source.FullName));
        Assert.Equal([null], Codes(candidates));
    }

    [Fact]
    public void Candidates_UseTheFolderOfEveryLocale()
    {
        Assert.Equal(
            [Path.Combine(_bed.Source, "hu-HU"), Path.Combine(_bed.Source, "en-US"), Path.Combine(_bed.Source, "de-DE")],
            _bed.Workspace.Candidates("hu-HU", "en-US").Select(candidate => candidate.Source.FullName));
    }
}
