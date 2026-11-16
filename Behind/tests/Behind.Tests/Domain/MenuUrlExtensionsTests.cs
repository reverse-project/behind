namespace Behind.Tests.Domain;

public class MenuUrlExtensionsTests
{
    [Theory]
    [InlineData("#/news")]
    [InlineData("#/")]
    [InlineData("/about")]
    [InlineData("//cdn.example.com/page")]
    [InlineData("page?x=1&y=2")]
    [InlineData("http://example.com")]
    [InlineData("HTTPS://example.com/a?b=c#d")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("tel:+3612345678")]
    public void IsSafeMenuUrl_RelativeLinksAndAllowedSchemes_AreSafe(string url)
    {
        Assert.True(url.IsSafeMenuUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com")]
    [InlineData("java\tscript:alert(1)")]
    [InlineData("java\nscript:alert(1)")]
    [InlineData(" javascript:alert(1)")]
    [InlineData("#/news and more")]
    [InlineData("http://example.com/\u0000")]
    public void IsSafeMenuUrl_UnsafeSchemesAndWhitespace_AreRejected(string? url)
    {
        Assert.False(url.IsSafeMenuUrl);
    }

    [Fact]
    public void IsSafeMenuUrl_LongerThanTheLimit_IsRejected()
    {
        Assert.True(new string('a', MenuItem.MaxUrlLength).IsSafeMenuUrl);
        Assert.False(new string('a', MenuItem.MaxUrlLength + 1).IsSafeMenuUrl);
    }
}
