namespace Behind.Tests.Domain;

public class DisplayNameExtensionsTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("\t\n", "")]
    [InlineData("  Ada Lovelace  ", "Ada Lovelace")]
    public void ToDisplayName_TrimsAndDefaultsToEmpty(string? input, string expected)
    {
        Assert.Equal(expected, input.ToDisplayName());
    }
}
