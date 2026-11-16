using Behind.MarkdownViewer.WebApi.Markdown;

namespace Behind.Tests.MarkdownViewer;

public class MarkdownRenderingExtensionsTests
{
    [Fact]
    public void Render_UsesTheFirstTopLevelHeadingAsTheTitle()
    {
        var page = "intro\n\n## Section\n\n# Real *title* with `code`\n\n# Second".Render("a/b.md", MarkdownLinks.Viewer);

        Assert.Equal("Real title with code", page.Title);
        Assert.Equal("a/b.md", page.Path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no heading here")]
    [InlineData("## only a second level")]
    public void Render_WithoutATopLevelHeading_UsesTheFileName(string markdown)
    {
        Assert.Equal("setup", markdown.Render("guide/setup.md", MarkdownLinks.Viewer).Title);
    }

    [Fact]
    public void TitleOf_ReadsTheTitleWithoutRendering()
    {
        Assert.Equal("Hello", "# Hello".TitleOf("a.md"));
        Assert.Equal("a", "text".TitleOf("a.md"));
    }

    [Fact]
    public void Render_ProducesAdvancedMarkdown()
    {
        var html = "| A | B |\n|---|:-:|\n| 1 | 2 |\n\n- [x] done\n\n```csharp\nvar a = 1;\n```\n\n## Heading".Render("a.md", MarkdownLinks.Viewer).Html;

        Assert.Contains("<table>", html);
        Assert.Contains("type=\"checkbox\"", html);
        Assert.Contains("language-csharp", html);
        Assert.Contains("<h2 id=\"heading\">Heading</h2>", html);
    }

    [Fact]
    public void Render_EscapesRawHtml()
    {
        var html = "<script>alert(1)</script>\n\n<div onclick=\"x()\">a</div>".Render("a.md", MarkdownLinks.Viewer).Html;

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<div onclick", html);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("![x](javascript:alert(1))")]
    public void Render_RemovesScriptLinks(string markdown)
    {
        Assert.DoesNotContain("javascript:", markdown.Render("a.md", MarkdownLinks.Viewer).Html);
    }

    [Fact]
    public void Render_RewritesLinksAndImages()
    {
        var html = "[doc](setup.md) [web](https://example.com/) ![pic](img/a.png)".Render("guide/index.md", MarkdownLinks.Viewer).Html;

        Assert.Contains("href=\"#/docs/guide%2Fsetup.md\"", html);
        Assert.Contains("href=\"https://example.com/\"", html);
        Assert.Contains("src=\"/markdownviewer/files/guide/img/a.png\"", html);
    }

    [Fact]
    public void Render_Static_ChangesDocumentLinksToHtml()
    {
        var html = "[doc](setup.md#install)".Render("guide/index.md", MarkdownLinks.Static).Html;

        Assert.Contains("href=\"setup.html#install\"", html);
    }
}
