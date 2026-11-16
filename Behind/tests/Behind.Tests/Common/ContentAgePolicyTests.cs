namespace Behind.Tests.Common;

public sealed class ContentAgePolicyTests
{
    [Theory]
    [InlineData(2008, 11, 12, 18, true)]
    [InlineData(2008, 11, 13, 18, false)]
    [InlineData(2008, 11, 11, 18, true)]
    [InlineData(2027, 1, 1, 1, false)]
    public void Allows_UsesInclusiveBirthdayBoundary(int year, int month, int day, int minimumAge, bool expected)
    {
        Assert.Equal(expected, ContentAgePolicy.Allows(
            minimumAge, new DateOnly(year, month, day), new DateOnly(2026, 11, 12)));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(18, false)]
    public void Allows_UnknownBirthdayOnlyAllowsUnrestrictedContent(int minimumAge, bool expected)
    {
        Assert.Equal(expected, ContentAgePolicy.Allows(minimumAge, null, new DateOnly(2026, 11, 12)));
    }

    [Fact]
    public void Age_LeapDayBirthdayUsesFebruary28InNonLeapYears()
    {
        Assert.Equal(17, ContentAgePolicy.Age(new DateOnly(2008, 2, 29), new DateOnly(2026, 2, 27)));
        Assert.Equal(18, ContentAgePolicy.Age(new DateOnly(2008, 2, 29), new DateOnly(2026, 2, 28)));
    }
}
