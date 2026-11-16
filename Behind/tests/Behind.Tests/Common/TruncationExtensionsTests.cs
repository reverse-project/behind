namespace Behind.Tests.Common;

public class TruncationExtensionsTests
{
    [Theory]
    [InlineData("short", 10, "short")]
    [InlineData("exactly ten", 11, "exactly ten")]
    [InlineData("a longer sentence", 10, "a longer…")]
    [InlineData("abcdefghij", 5, "abcd…")]
    [InlineData("", 5, "")]
    public void Shortened_CutsLongTextAndAddsAnEllipsis(string text, int maxLength, string expected)
    {
        Assert.Equal(expected, text.Shortened(maxLength));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Shortened_LeavesTextAloneWhenThereIsNoRoomForAnEllipsis(int maxLength)
    {
        Assert.Equal("text", "text".Shortened(maxLength));
    }
}
