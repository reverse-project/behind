namespace Behind.Tests.Common;

public class SearchTermExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToSearchTerm_BlankValue_ReturnsNone(string? value)
    {
        Assert.Equal(None, value.ToSearchTerm(10));
    }

    [Fact]
    public void ToSearchTerm_TrimsTheValue()
    {
        Assert.Equal(Some("ada"), "  ada ".ToSearchTerm(10));
    }

    [Fact]
    public void ToSearchTerm_TruncatesToTheMaximumLength()
    {
        Assert.Equal(Some("abc"), "abcdef".ToSearchTerm(3));
    }
}
