using Behind.MarkdownViewer.WebApi.Markdown;

namespace Behind.Tests.MarkdownViewer;

public class MarkdownLinkExtensionsTests
{
    [Theory]
    [InlineData("guide/index.md", "setup.md", "#/docs/guide%2Fsetup.md")]
    [InlineData("guide/index.md", "../README.md", "#/docs/README.md")]
    [InlineData("guide/index.md", "./setup.md#install", "#/docs/guide%2Fsetup.md")]
    [InlineData("README.md", "guide/a%20b.md", "#/docs/guide%2Fa%20b.md")]
    [InlineData("README.md", "GUIDE.MD?x=1", "#/docs/GUIDE.MD")]
    public void Rewrite_Viewer_PointsDocumentsAtTheViewerRoute(string document, string url, string expected)
    {
        Assert.Equal(expected, MarkdownLinks.Viewer.Rewrite(document, url));
    }

    [Theory]
    [InlineData("guide/index.md", "img/a.png", "/markdownviewer/files/guide/img/a.png")]
    [InlineData("guide/index.md", "../img/a b.png", "/markdownviewer/files/img/a%20b.png")]
    [InlineData("README.md", "a.png?x=1#y", "/markdownviewer/files/a.png")]
    public void Rewrite_Viewer_PointsAssetsAtTheFilesRoute(string document, string url, string expected)
    {
        Assert.Equal(expected, MarkdownLinks.Viewer.Rewrite(document, url));
    }

    [Theory]
    [InlineData("setup.md", "setup.html")]
    [InlineData("../README.md", "../README.html")]
    [InlineData("setup.md#install", "setup.html#install")]
    [InlineData("setup.md?x=1", "setup.html?x=1")]
    [InlineData("a%20b.md", "a%20b.html")]
    public void Rewrite_Static_ChangesTheExtensionOfDocuments(string url, string expected)
    {
        Assert.Equal(expected, MarkdownLinks.Static.Rewrite("guide/index.md", url));
    }

    [Theory]
    [InlineData("img/a.png")]
    [InlineData("../img/a.png#y")]
    public void Rewrite_Static_KeepsAssetLinks(string url)
    {
        Assert.Equal(url, MarkdownLinks.Static.Rewrite("guide/index.md", url));
    }

    [Theory]
    [InlineData("")]
    [InlineData("#section")]
    [InlineData("/absolute/path.md")]
    [InlineData("//cdn.example.com/a.md")]
    [InlineData("https://example.com/a.md")]
    [InlineData("mailto:a@b.c")]
    [InlineData("javascript:alert(1)")]
    [InlineData("../../outside.md")]
    [InlineData("?query")]
    public void Rewrite_LeavesOtherLinksAlone(string url)
    {
        Assert.Equal(url, MarkdownLinks.Viewer.Rewrite("guide/index.md", url));
        Assert.Equal(url, MarkdownLinks.Static.Rewrite("guide/index.md", url));
    }

    [Fact]
    public void ToHtmlPath_ReplacesTheMarkdownExtension()
    {
        Assert.Equal("guide/setup.html", "guide/setup.md".ToHtmlPath());
        Assert.Equal("GUIDE.html", "GUIDE.MD".ToHtmlPath());
    }

    private static readonly string[] Locales = ["en-US", "hu-HU"];

    [Theory]
    [InlineData("en-US", "guide/index.md", "setup.md", "#/docs/guide%2Fsetup.md")]
    [InlineData("en-US", "guide/index.md", "../README.md", "#/docs/README.md")]
    [InlineData("en-US", "README.md", "../README.md", "#/docs/~%2FREADME.md")]
    [InlineData("en-US", "guide/index.md", "../../README.md", "#/docs/~%2FREADME.md")]
    [InlineData("en-US", "README.md", "../hu-HU/README.md", "#/docs/README.md?locale=hu-HU")]
    [InlineData("en-US", "guide/index.md", "../../hu-HU/guide/setup.md", "#/docs/guide%2Fsetup.md?locale=hu-HU")]
    [InlineData("en-US", "README.md", "../en-US/guide.md", "#/docs/guide.md")]
    public void Rewrite_Localized_ResolvesDocumentsAcrossLocalesAndTheSharedRoot(string locale, string document, string url, string expected)
    {
        Assert.Equal(expected, MarkdownLinks.ViewerFor(locale, Locales).Rewrite(document, url));
    }

    [Theory]
    [InlineData("README.md", "./en-US/README.md", "#/docs/README.md?locale=en-US")]
    [InlineData("README.md", "hu-HU/developer/README.md", "#/docs/developer%2FREADME.md?locale=hu-HU")]
    [InlineData("README.md", "CONTRIBUTING.md", "#/docs/~%2FCONTRIBUTING.md")]
    public void Rewrite_Localized_FromASharedDocument(string document, string url, string expected)
    {
        Assert.Equal(expected, MarkdownLinks.ViewerFor(null, Locales).Rewrite("~/" + document, url));
    }

    [Theory]
    [InlineData("../../outside.md")]
    [InlineData("../../../outside.md")]
    [InlineData("../logo.png")]
    [InlineData("../other/readme.png")]
    public void Rewrite_Localized_LeavesLinksOutsideTheDocumentationAlone(string url)
    {
        Assert.Equal(url, MarkdownLinks.ViewerFor("en-US", Locales).Rewrite("README.md", url));
    }

    [Fact]
    public void Rewrite_Localized_PointsAssetsAtTheirLocale()
    {
        var links = MarkdownLinks.ViewerFor("en-US", Locales);

        Assert.Equal("/markdownviewer/files/img/a.png?locale=en-US", links.Rewrite("guide/index.md", "../img/a.png"));
        Assert.Equal("/markdownviewer/files/a.png?locale=hu-HU", links.Rewrite("README.md", "../hu-HU/a.png"));
    }

    [Theory]
    [InlineData("../README.md", "../README.html")]
    [InlineData("../hu-HU/README.md", "../hu-HU/README.html")]
    [InlineData("setup.md#a", "setup.html#a")]
    public void Rewrite_StaticLocalized_KeepsTheRelativeLinksOfTheOutput(string url, string expected)
    {
        Assert.Equal(expected, MarkdownLinks.StaticFor("en-US").Rewrite("guide.md", url.StartsWith("../") ? url : url));
    }
}
