namespace Behind.Tests.Common;

public class LetterBucketExtensionsTests
{
    [Fact]
    public void All_IsTheAlphabetFollowedByTheOtherBucket()
    {
        Assert.Equal(27, LetterBucketExtensions.All.Count);
        Assert.Equal("A", LetterBucketExtensions.All[0]);
        Assert.Equal("Z", LetterBucketExtensions.All[25]);
        Assert.Equal("#", LetterBucketExtensions.All[26]);
    }

    [Theory]
    [InlineData("a", "A")]
    [InlineData(" z ", "Z")]
    [InlineData("#", "#")]
    public void ParseLetterBucket_NormalizesValidBuckets(string value, string expected)
    {
        Assert.Equal(Some(expected), LetterBucketExtensions.ParseLetterBucket(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("1")]
    [InlineData("é")]
    public void ParseLetterBucket_RejectsEverythingElse(string? value)
    {
        Assert.Equal(None, LetterBucketExtensions.ParseLetterBucket(value));
    }

    [Theory]
    [InlineData("ada", "A")]
    [InlineData("Zed", "Z")]
    [InlineData("42nd", "#")]
    [InlineData("Émile", "#")]
    [InlineData("", "#")]
    [InlineData(null, "#")]
    public void ToLetterBucket_UsesTheFirstCharacterOrTheOtherBucket(string? label, string expected)
    {
        Assert.Equal(expected, label.ToLetterBucket());
    }
}
