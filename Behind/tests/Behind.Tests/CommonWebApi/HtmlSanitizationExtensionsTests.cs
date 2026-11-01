using System.Text;
using Behind.Common.WebApi;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CommonWebApi;

public class HtmlSanitizationExtensionsTests
{
    [Theory]
    [InlineData("<p>Hello <strong>world</strong></p>")]
    [InlineData("<h2>Title</h2><ul><li>one</li></ul><ol><li>two</li></ol>")]
    [InlineData("<blockquote>quote</blockquote><pre class=\"ql-syntax\">code</pre>")]
    [InlineData("<p class=\"ql-align-center ql-indent-1\">centered</p>")]
    [InlineData("<p><a href=\"https://example.com/\">link</a> <a href=\"mailto:a@b.c\">mail</a></p>")]
    [InlineData("<p><img src=\"/sites/images/abc\" alt=\"logo\"></p>")]
    public void SanitizeRichText_KeepsTheEditorMarkup(string html)
    {
        Assert.Equal(html, html.SanitizeRichText());
    }

    [Theory]
    [InlineData("<p>a</p><script>alert(1)</script>", "<p>a</p>")]
    [InlineData("<p onclick=\"alert(1)\">a</p>", "<p>a</p>")]
    [InlineData("<p style=\"color:red\">a</p>", "<p>a</p>")]
    [InlineData("<a href=\"javascript:alert(1)\">a</a>", "<a>a</a>")]
    [InlineData("<img src=\"data:image/png;base64,AAAA\">", "<img>")]
    [InlineData("<iframe src=\"https://example.com\"></iframe><p>a</p>", "<p>a</p>")]
    [InlineData("<p class=\"evil\">a</p>", "<p>a</p>")]
    [InlineData("<img src=\"x\" onerror=\"alert(1)\">", "<img src=\"x\">")]
    public void SanitizeRichText_RemovesAnythingUnsafe(string html, string expected)
    {
        Assert.Equal(expected, html.SanitizeRichText());
    }

    [Fact]
    public void SanitizeRichText_EmptyContent_StaysEmpty()
    {
        Assert.Equal(string.Empty, string.Empty.SanitizeRichText());
    }
}

public class FormFileExtensionsTests
{
    [Fact]
    public async Task ReadAllBytesAsync_ReturnsTheWholeFile()
    {
        byte[] bytes = [1, 2, 3, 4, 5];
        using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "a.bin");

        Assert.Equal(bytes, await file.ReadAllBytesAsync());
    }

    [Fact]
    public async Task ReadAllBytesAsync_EmptyFile_ReturnsNothing()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Empty));
        var file = new FormFile(stream, 0, 0, "file", "a.bin");

        Assert.Empty(await file.ReadAllBytesAsync());
    }
}
