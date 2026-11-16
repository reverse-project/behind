using Behind.MarkdownViewer.Domain;
using Behind.MarkdownViewer.WebApi.StaticSite;

namespace Behind.Tests.MarkdownViewer;

public sealed class StaticSiteExtensionsTests : IDisposable
{
    private readonly MarkdownSourceTestBed _bed = new MarkdownSourceTestBed()
        .With("README.md", "# Welcome\n\nSee [setup](guide/setup.md).")
        .With("guide/setup.md", "# Setup\n\nBack to [home](../README.md) ![logo](../img/logo.png)\n\n<script>alert(1)</script>")
        .With("notes.txt", "text")
        .With(".git/secret.md", "# Hidden")
        .WithBytes("img/logo.png", [1, 2, 3])
        .WithBytes("img/run.exe", [1, 2, 3]);

    public void Dispose() => _bed.Dispose();

    [Fact]
    public async Task Generate_WritesAPagePerDocumentAndCopiesTheAssets()
    {
        var site = await _bed.Generation.Generate(null, CancellationToken.None);

        Assert.Equal(new GeneratedSite(_bed.Output, 2, 1, 0), site);
        Assert.True(_bed.OutputExists("README.html"));
        Assert.True(_bed.OutputExists("guide/setup.html"));
        Assert.True(_bed.OutputExists("img/logo.png"));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(_bed.Output, "img", "logo.png")));
        Assert.False(_bed.OutputExists("img/run.exe"));
        Assert.False(_bed.OutputExists("notes.txt"));
        Assert.False(_bed.OutputExists(".git/secret.html"));
    }

    [Fact]
    public async Task Generate_WritesTheStyleAndTheManifest()
    {
        await _bed.Generation.Generate(null, CancellationToken.None);

        Assert.Equal(StaticSiteStyle.Css, _bed.ReadOutput(StaticSiteExtensions.StyleFile));
        string[] expected =
            ["README.html", "guide/setup.html", "img/logo.png", StaticSiteExtensions.StyleFile, "index.html"];

        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            File.ReadAllLines(Path.Combine(_bed.Output, StaticSiteExtensions.ManifestFile)));
    }

    [Fact]
    public async Task Generate_RendersTheLayoutWithRelativeLinks()
    {
        await _bed.Generation.Generate(null, CancellationToken.None);

        var home = _bed.ReadOutput("README.html");
        var setup = _bed.ReadOutput("guide/setup.html");

        Assert.Contains("<title>Welcome - Docs</title>", home);
        Assert.Contains("href=\"markdownviewer.css\"", home);
        Assert.Contains("href=\"guide/setup.html\"", home);
        Assert.Contains("<li class=\"section\">Guide</li>", home);
        Assert.Contains("<li class=\"depth-0\"><a href=\"guide/setup.html\">Setup</a></li>", home);
        Assert.DoesNotContain("<a href=\"README.html\"", home);
        Assert.Contains("href=\"../markdownviewer.css\"", setup);
        Assert.Contains("<a href=\"../index.html\">Docs</a>", setup);
        Assert.DoesNotContain("<a href=\"../README.html\">Welcome</a>", setup);
        Assert.Contains("<a href=\"../guide/setup.html\" aria-current=\"page\">Setup</a>", setup);
        Assert.Contains("href=\"../README.html\"", setup);
        Assert.Contains("src=\"../img/logo.png\"", setup);
        Assert.DoesNotContain("<script", setup);
    }

    [Fact]
    public async Task Generate_RedirectsTheIndexToTheDefaultDocument()
    {
        await _bed.Generation.Generate(null, CancellationToken.None);

        Assert.Contains("url=README.html", _bed.ReadOutput("index.html"));
    }

    [Fact]
    public async Task Generate_RedirectsTheIndexToTheFirstDocumentWithoutTheDefaultOne()
    {
        File.Delete(Path.Combine(_bed.Source, "README.md"));

        await _bed.Generation.Generate(null, CancellationToken.None);

        Assert.Contains("url=guide/setup.html", _bed.ReadOutput("index.html"));
    }

    [Fact]
    public async Task Generate_KeepsADocumentNamedIndex()
    {
        _bed.With("index.md", "# Start");

        await _bed.Generation.Generate(null, CancellationToken.None);

        Assert.Contains("<title>Start - Docs</title>", _bed.ReadOutput("index.html"));
    }

    [Fact]
    public async Task Generate_WithoutDocuments_WritesOnlyTheStyle()
    {
        Directory.Delete(_bed.Source, true);
        Directory.CreateDirectory(_bed.Source);

        var site = await _bed.Generation.Generate(null, CancellationToken.None);

        Assert.Equal(new GeneratedSite(_bed.Output, 0, 0, 0), site);
        Assert.False(_bed.OutputExists("index.html"));
        Assert.True(_bed.OutputExists(StaticSiteExtensions.StyleFile));
    }

    [Fact]
    public async Task Generate_RemovesTheFilesOfDeletedDocumentsAndEmptyDirectories()
    {
        await _bed.Generation.Generate(null, CancellationToken.None);
        File.Delete(Path.Combine(_bed.Source, "guide", "setup.md"));

        var site = await _bed.Generation.Generate(null, CancellationToken.None);

        Assert.Equal(1, site.Removed);
        Assert.False(_bed.OutputExists("guide/setup.html"));
        Assert.False(Directory.Exists(Path.Combine(_bed.Output, "guide")));
        Assert.True(_bed.OutputExists("README.html"));
    }

    [Fact]
    public async Task Generate_LeavesUnrelatedOutputFilesAlone()
    {
        Directory.CreateDirectory(_bed.Output);
        File.WriteAllText(Path.Combine(_bed.Output, "CNAME"), "docs.example.com");

        await _bed.Generation.Generate(null, CancellationToken.None);
        await _bed.Generation.Generate(null, CancellationToken.None);

        Assert.Equal("docs.example.com", _bed.ReadOutput("CNAME"));
    }

    [Fact]
    public async Task Generate_IgnoresManifestEntriesOutsideTheOutputDirectory()
    {
        var outside = Path.Combine(_bed.Root, "outside.txt");
        File.WriteAllText(outside, "keep");
        Directory.CreateDirectory(_bed.Output);
        File.WriteAllLines(Path.Combine(_bed.Output, StaticSiteExtensions.ManifestFile), ["../outside.txt", ""]);

        var site = await _bed.Generation.Generate(null, CancellationToken.None);

        Assert.Equal(0, site.Removed);
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task Generate_OverwritesPreviousPages()
    {
        await _bed.Generation.Generate(null, CancellationToken.None);
        _bed.With("README.md", "# Changed");

        await _bed.Generation.Generate(null, CancellationToken.None);

        Assert.Contains("<title>Changed - Docs</title>", _bed.ReadOutput("README.html"));
    }

    [Fact]
    public async Task Generate_RunsConcurrentRequestsOneAfterTheOther()
    {
        var sites = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => _bed.Generation.Generate(null, CancellationToken.None)));

        Assert.All(sites, site => Assert.Equal(2, site.Pages));
        Assert.True(_bed.OutputExists("README.html"));
    }

    [Fact]
    public async Task Generate_EscapesTitlesAndUrlsOfTheNavigation()
    {
        _bed.With("a b/<c>.md", "# Tom & <Jerry>");

        await _bed.Generation.Generate(null, CancellationToken.None);

        var home = _bed.ReadOutput("README.html");
        Assert.Contains("href=\"a%20b/%3Cc%3E.html\"", home);
        Assert.Contains("Tom &amp; &lt;Jerry&gt;", home);
    }

    private MarkdownSourceTestBed Localized() => new MarkdownSourceTestBed()
        .With("README.md", "# All languages\n\n[English](en-US/README.md)")
        .With("en-US/README.md", "# Welcome\n\nSee [setup](guide/setup.md) ![logo](logo.png). [All languages](../README.md)")
        .With("en-US/guide/setup.md", "# Setup")
        .WithBytes("en-US/logo.png", [1, 2, 3])
        .With("hu-HU/README.md", "# Üdv")
        .WithBytes("hu-HU/logo.png", [4, 5, 6])
        .With("de-DE/.keep", "no documents");

    [Fact]
    public async Task Generate_WithLocaleFolders_WritesOneSitePerLocale()
    {
        using var bed = Localized();

        var site = await bed.Generation.Generate("en-US", CancellationToken.None);

        Assert.Equal(new GeneratedSite(bed.Output, 4, 2, 0), site);
        Assert.True(bed.OutputExists("en-US/README.html"));
        Assert.True(bed.OutputExists("en-US/guide/setup.html"));
        Assert.True(bed.OutputExists("hu-HU/README.html"));
        Assert.True(bed.OutputExists("en-US/logo.png"));
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(Path.Combine(bed.Output, "hu-HU", "logo.png")));
        Assert.False(bed.OutputExists("de-DE/index.html"));
        Assert.Contains("url=README.html", bed.ReadOutput("en-US/index.html"));
        Assert.Equal(StaticSiteStyle.Css, bed.ReadOutput(StaticSiteExtensions.StyleFile));
    }

    [Fact]
    public async Task Generate_WithLocaleFolders_LinksTheStylesheetAndTheOtherLanguages()
    {
        using var bed = Localized();

        await bed.Generation.Generate("en-US", CancellationToken.None);

        var home = bed.ReadOutput("en-US/README.html");
        var setup = bed.ReadOutput("en-US/guide/setup.html");
        var hungarian = bed.ReadOutput("hu-HU/README.html");

        Assert.Contains("<html lang=\"en-US\">", home);
        Assert.Contains("href=\"../markdownviewer.css\"", home);
        Assert.Contains("href=\"../../markdownviewer.css\"", setup);
        Assert.Contains("<a href=\"index.html\">Docs</a>", home);
        Assert.Contains("<a href=\"../index.html\">Docs</a>", setup);
        Assert.Contains("hreflang=\"hu-HU\" href=\"../hu-HU/README.html\"", home);
        Assert.Contains("hreflang=\"hu-HU\" href=\"../../hu-HU/index.html\"", setup);
        Assert.Contains("lang=\"en-US\" aria-current=\"true\"", home);
        Assert.Contains("hreflang=\"en-US\" href=\"../en-US/README.html\"", hungarian);
        Assert.DoesNotContain("de-DE", home);
        Assert.Contains("src=\"logo.png\"", home);
    }

    [Fact]
    public async Task Generate_WithLocaleFolders_RedirectsTheRootToTheDefaultLocale()
    {
        using var bed = Localized();

        await bed.Generation.Generate("hu-HU", CancellationToken.None);

        var index = bed.ReadOutput("index.html");
        Assert.Contains("\"hu-HU\"", index);
        Assert.Contains("url=hu-HU/index.html", index);
        Assert.Contains("[\"de-DE\", \"en-US\", \"hu-HU\"]", index);
        Assert.Contains("href=\"en-US/index.html\"", index);
    }

    [Theory]
    [InlineData(null, "de-DE")]
    [InlineData("xx-XX", "de-DE")]
    public async Task Generate_WithLocaleFolders_FallsBackToTheFirstLocaleForTheRoot(string? defaultLocale, string expected)
    {
        using var bed = Localized();

        await bed.Generation.Generate(defaultLocale, CancellationToken.None);

        Assert.Contains($"url={expected}/index.html", bed.ReadOutput("index.html"));
    }

    [Fact]
    public async Task Generate_WithLocaleFolders_RemovesTheFilesOfADeletedLocale()
    {
        using var bed = Localized();
        await bed.Generation.Generate("en-US", CancellationToken.None);
        Directory.Delete(Path.Combine(bed.Source, "hu-HU"), true);

        var site = await bed.Generation.Generate("en-US", CancellationToken.None);

        Assert.Equal(3, site.Removed);
        Assert.False(Directory.Exists(Path.Combine(bed.Output, "hu-HU")));
        Assert.True(bed.OutputExists("en-US/README.html"));
    }

    [Fact]
    public async Task Generate_WithoutLocaleFolders_HasNoSwitcherAndNoLanguageAttributeOfALocale()
    {
        await _bed.Generation.Generate(null, CancellationToken.None);

        var home = _bed.ReadOutput("README.html");
        Assert.DoesNotContain("class=\"languages\"", home);
        Assert.Contains("<html lang=\"en\">", home);
    }

    [Fact]
    public async Task Generate_WithLocaleFolders_WritesTheRootDocumentsAsSharedPages()
    {
        using var bed = Localized();

        await bed.Generation.Generate("en-US", CancellationToken.None);

        var shared = bed.ReadOutput("README.html");
        var english = bed.ReadOutput("en-US/README.html");
        Assert.Contains("<title>All languages - Docs</title>", shared);
        Assert.Contains("href=\"en-US/README.html\"", shared);
        Assert.Contains("href=\"markdownviewer.css\"", shared);
        Assert.DoesNotContain("class=\"languages\"", shared);
        Assert.Contains("href=\"../README.html\"", english);
        Assert.Contains("url=en-US/index.html", bed.ReadOutput("index.html"));
    }
}
