using static Behind.Common.FunctionalExtensions;

namespace Behind.Tests.Common;

public class Base64ExtensionsTests
{
    [Theory]
    [InlineData("SGVsbG8=")]
    [InlineData("SGVsbG8")]
    public void FromBase64Text_DecodesWithAndWithoutPadding(string encoded)
    {
        Assert.Equal(Some("Hello"), encoded.FromBase64Text());
    }

    [Fact]
    public void FromBase64_AcceptsTheUrlSafeAlphabet()
    {
        byte[] bytes = [0xFB, 0xFF, 0xBE];

        Assert.Equal(bytes, "-_--".FromBase64().GetOrElse([]));
        Assert.Equal(bytes, "+/++".FromBase64().GetOrElse([]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not base64!")]
    [InlineData("A")]
    public void FromBase64_ReturnsNoneForInvalidInput(string? encoded)
    {
        Assert.Equal(None, encoded.FromBase64());
    }

    [Fact]
    public void FromBase64Text_ReturnsNoneWhenTheBytesAreNotUtf8()
    {
        var encoded = Convert.ToBase64String([0xFF, 0xFE, 0xFD]);

        Assert.Equal(None, encoded.FromBase64Text());
    }
}
