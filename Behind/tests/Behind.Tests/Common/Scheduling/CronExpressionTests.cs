using System.ComponentModel;
using System.Text.Json;
using Behind.Common.Scheduling;

namespace Behind.Tests.Common.Scheduling;

public sealed class CronExpressionTests
{
    private static readonly DateTimeOffset Monday = new(2026, 11, 16, 10, 30, 0, TimeSpan.Zero);

    private static CronExpression Cron(string text) =>
        CronExpression.TryParse(text).Match(() => throw new InvalidOperationException(text), cron => cron);

    private static DateTimeOffset NextOf(string text, DateTimeOffset after) =>
        Cron(text).Next(after).Match(() => throw new InvalidOperationException(text), next => next);

    [Theory]
    [InlineData("* * * * *")]
    [InlineData("*/15 0-6,18-23 1,15 jan-mar,dec mon-fri")]
    [InlineData("0 12 ? * SUN")]
    [InlineData("5 4 * * 7")]
    [InlineData("  0   0   1   1   *  ")]
    [InlineData("10-50/20 * * * *")]
    public void Parse_AcceptsStandardFiveFieldExpressions(string text)
    {
        Assert.True(CronExpression.Parse(text).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("* * * *")]
    [InlineData("* * * * * *")]
    [InlineData("60 * * * *")]
    [InlineData("* 24 * * *")]
    [InlineData("* * 0 * *")]
    [InlineData("* * 32 * *")]
    [InlineData("* * * 13 *")]
    [InlineData("* * * * 8")]
    [InlineData("*/0 * * * *")]
    [InlineData("5-1 * * * *")]
    [InlineData("a * * * *")]
    [InlineData("1,,2 * * * *")]
    [InlineData("* * * foo *")]
    public void Parse_RejectsMalformedExpressions(string? text)
    {
        Assert.False(CronExpression.Parse(text).IsValid);
        Assert.True(CronExpression.TryParse(text).IsNone());
    }

    [Fact]
    public void Parse_NormalizesWhitespaceAndKeepsTheFieldTexts()
    {
        var cron = Cron("  */5   9-17 * * mon-fri ");

        Assert.Equal("*/5 9-17 * * mon-fri", cron.Text);
        Assert.Equal(
            ["*/5", "9-17", "*", "*", "mon-fri"],
            [cron.Minute.Text, cron.Hour.Text, cron.DayOfMonth.Text, cron.Month.Text, cron.DayOfWeek.Text]);
        Assert.Equal([0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55], cron.Minute.Values);
        Assert.Equal([1, 2, 3, 4, 5], cron.DayOfWeek.Values);
    }

    [Fact]
    public void Parse_TreatsSevenAsSunday()
    {
        Assert.Equal([0], Cron("0 0 * * 7").DayOfWeek.Values);
        Assert.Equal([0, 5, 6], Cron("0 0 * * 5-7").DayOfWeek.Values);
    }

    [Fact]
    public void Matches_ComparesInUtcAndIgnoresSeconds()
    {
        var cron = Cron("30 10 * * *");

        Assert.True(cron.Matches(Monday.AddSeconds(42)));
        Assert.True(cron.Matches(Monday.ToOffset(TimeSpan.FromHours(2))));
        Assert.False(cron.Matches(Monday.AddMinutes(1)));
    }

    [Fact]
    public void Matches_CombinesRestrictedDayOfMonthAndDayOfWeekWithOr()
    {
        var cron = Cron("0 0 1 * fri");

        Assert.True(cron.Matches(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.True(cron.Matches(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(cron.Matches(new DateTimeOffset(2026, 11, 19, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Matches_UsesOnlyTheRestrictedDayField()
    {
        Assert.True(Cron("0 0 * * mon").Matches(new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(Cron("0 0 * * mon").Matches(new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero)));
        Assert.True(Cron("0 0 17 * ?").Matches(new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Matches_TreatsSteppedWildcardDayFieldsAsWildcards()
    {
        var everyMonday = Cron("0 0 */1 * mon");
        var firstOnSteppedWeekdays = Cron("0 0 1 * */3");

        Assert.True(everyMonday.DayOfMonth.IsWildcard);
        Assert.True(everyMonday.Matches(new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(everyMonday.Matches(new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero)));
        Assert.True(firstOnSteppedWeekdays.DayOfWeek.IsWildcard);
        Assert.False(firstOnSteppedWeekdays.Matches(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(firstOnSteppedWeekdays.Matches(new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero)));
        Assert.True(firstOnSteppedWeekdays.Matches(new DateTimeOffset(2027, 5, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Next_TreatsSteppedWildcardDayFieldsAsWildcards()
    {
        Assert.Equal(new DateTimeOffset(2026, 11, 23, 0, 0, 0, TimeSpan.Zero), NextOf("0 0 */1 * mon", Monday));
        Assert.Equal(new DateTimeOffset(2027, 5, 1, 0, 0, 0, TimeSpan.Zero), NextOf("0 0 1 * */3", Monday));
    }

    [Fact]
    public void Next_ReturnsTheFirstMatchingMinuteStrictlyAfterTheMoment()
    {
        Assert.Equal(Monday.AddMinutes(1), NextOf("* * * * *", Monday));
        Assert.Equal(Monday.AddMinutes(15), NextOf("*/15 * * * *", Monday));
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 10, 30, 0, TimeSpan.Zero), NextOf("30 10 * * *", Monday));
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 10, 31, 0, TimeSpan.Zero), NextOf("* * * * *", Monday.AddSeconds(10)));
    }

    [Fact]
    public void Next_CrossesMonthsAndYearsAndSkipsMissingDays()
    {
        Assert.Equal(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), NextOf("0 0 1 1 *", Monday));
        Assert.Equal(new DateTimeOffset(2028, 2, 29, 0, 0, 0, TimeSpan.Zero), NextOf("0 0 29 2 *", Monday));
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero), NextOf("0 0 31 * *", Monday));
    }

    [Fact]
    public void Next_ReturnsNoneForImpossibleDates()
    {
        Assert.True(Cron("0 0 30 2 *").Next(Monday).IsNone());
    }

    [Fact]
    public void Occurrences_ListsConsecutiveRuns()
    {
        var runs = Cron("0 */6 * * *").Occurrences(Monday, 3);

        Assert.Equal(
            [
                new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 16, 18, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero)
            ],
            runs);
        Assert.Empty(Cron("0 0 30 2 *").Occurrences(Monday, 3));
    }

    [Fact]
    public void Equality_UsesTheNormalizedText()
    {
        Assert.Equal(Cron("0  0 * * *"), Cron("0 0 * * *"));
        Assert.NotEqual(Cron("0 0 * * *"), Cron("0 1 * * *"));
    }

    [Fact]
    public void Converters_RoundTripTheText()
    {
        var cron = Cron("*/5 * * * *");
        var json = JsonSerializer.Serialize(cron);

        Assert.Equal("\"*/5 * * * *\"", json);
        Assert.Equal(cron, JsonSerializer.Deserialize<CronExpression>(json));
        Assert.Equal(cron, TypeDescriptor.GetConverter(typeof(CronExpression)).ConvertFromInvariantString("*/5 * * * *"));
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<CronExpression>("\"nope\""));
    }
}
