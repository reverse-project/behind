using Behind.Common.WebApi.Infrastructure.StaticFiles;

namespace Behind.Tests.CommonWebApi.StaticFiles;

public class ContentMinifierTests
{
    [Fact]
    public void JavaScriptMinifier_ValidSource_RemovesWhitespaceAndComments()
    {
        var result = new JavaScriptMinifier().Minify("// note\nvar total = 1 + 2;\n\nconsole.log(total);\n");

        var minified = result.GetOrElse(string.Empty);
        Assert.NotEmpty(minified);
        Assert.DoesNotContain("note", minified);
        Assert.DoesNotContain("\n", minified);
    }

    [Fact]
    public void JavaScriptMinifier_InvalidSource_ReturnsNone()
    {
        Assert.Equal((Option<string>)None, new JavaScriptMinifier().Minify("function ("));
    }

    [Fact]
    public void CssMinifier_ValidSource_RemovesWhitespace()
    {
        var minified = new CssMinifier().Minify("body {\n    margin: 0;\n}\n").GetOrElse(string.Empty);

        Assert.Equal("body{margin:0}", minified);
    }

    [Fact]
    public void HtmlMinifier_ValidSource_CollapsesWhitespaceAndRemovesComments()
    {
        var minified = new HtmlMinifier()
            .Minify("<div>\n    <!-- hidden -->\n    <p>text</p>\n</div>\n")
            .GetOrElse(string.Empty);

        Assert.Contains("<p>text", minified);
        Assert.DoesNotContain("hidden", minified);
        Assert.DoesNotContain("\n", minified);
    }

    [Fact]
    public void HtmlMinifier_KnockoutBindings_ArePreserved()
    {
        var minified = new HtmlMinifier()
            .Minify("<button data-bind=\"enable: !busy(), click: save\">Go</button>")
            .GetOrElse(string.Empty);

        Assert.Contains("enable: !busy(), click: save", minified);
    }

    [Fact]
    public void HtmlMinifier_TableWithKnockoutComments_KeepsTheOptionalEndTags()
    {
        var minified = new HtmlMinifier()
            .Minify("<table><tbody><tr><td>a</td><!-- ko foreach: cells --><td>b</td><!-- /ko --></tr></tbody></table>")
            .GetOrElse(string.Empty);

        Assert.Contains("</td><!-- /ko --></tr></tbody></table>", minified);
    }

    [Theory]
    [InlineData(".js")]
    [InlineData(".MJS")]
    [InlineData(".css")]
    [InlineData(".html")]
    [InlineData(".htm")]
    public void Default_CoversExtension(string extension)
    {
        Assert.Contains(ContentMinifiers.Default, minifier => minifier.Extensions.Contains(extension));
    }

    [Fact]
    public void Default_DoesNotCoverImages()
    {
        Assert.DoesNotContain(ContentMinifiers.Default, minifier => minifier.Extensions.Contains(".png"));
    }
}
