namespace Behind.Tests.Common;

public class SlugExtensionsTests
{
    [Theory]
    [InlineData("About us", "about-us")]
    [InlineData("  Hello,   World!  ", "hello-world")]
    [InlineData("Crème brûlée", "creme-brulee")]
    [InlineData("--already--slugged--", "already-slugged")]
    [InlineData("Version 2.0", "version-2-0")]
    [InlineData("ABC123", "abc123")]
    public void ToSlug_NormalizesTheText(string value, string expected)
    {
        Assert.Equal(Some(expected), value.ToSlug(100));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("日本語")]
    public void ToSlug_WithoutUsableCharacters_ReturnsNone(string? value)
    {
        Assert.Equal(None, value.ToSlug(100));
    }

    [Fact]
    public void ToSlug_TruncatesWithoutLeavingATrailingSeparator()
    {
        Assert.Equal(Some("abc"), "abc-def".ToSlug(4));
        Assert.Equal(Some("abcd"), "abcdef".ToSlug(4));
    }
}

public class OptionNullableExtensionsTests
{
    [Fact]
    public void ToNullable_Some_ReturnsTheValue()
    {
        Assert.Equal(5, Some(5).ToNullable());
    }

    [Fact]
    public void ToNullable_None_ReturnsNull()
    {
        Option<long> none = None;

        Assert.Null(none.ToNullable());
    }
}
