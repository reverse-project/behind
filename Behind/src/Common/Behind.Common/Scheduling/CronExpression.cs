using System.ComponentModel;
using System.Text.Json.Serialization;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Scheduling;

[TypeConverter(typeof(CronExpressionTypeConverter))]
[JsonConverter(typeof(CronExpressionJsonConverter))]
public sealed class CronExpression : IEquatable<CronExpression>
{
    private const int FieldCount = 5;
    private const int SearchYears = 8;

    private CronExpression(string text, IReadOnlyList<CronField> fields)
    {
        Text = text;
        Minute = fields[0];
        Hour = fields[1];
        DayOfMonth = fields[2];
        Month = fields[3];
        DayOfWeek = fields[4];
    }

    public string Text { get; }

    public CronField Minute { get; }

    public CronField Hour { get; }

    public CronField DayOfMonth { get; }

    public CronField Month { get; }

    public CronField DayOfWeek { get; }

    public static Validation<CronExpression> Parse(string? text)
    {
        var normalized = string.Join(' ', (text ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != FieldCount)
        {
            return Invalid<CronExpression>(Error($"Expected {FieldCount} fields but found {parts.Length}"));
        }

        var fields = parts
            .Zip(CronFieldSpecification.All, CronField.Parse)
            .ToList();
        var errors = fields
            .SelectMany(field => field.Match(problems => problems, _ => Enumerable.Empty<Error>()))
            .ToList();

        return errors.Count > 0
            ? Invalid<CronExpression>(errors)
            : Valid(new CronExpression(
                normalized,
                fields.Select(field => field.Match(_ => throw new InvalidOperationException(), value => value)).ToList()));
    }

    public static Option<CronExpression> TryParse(string? text) =>
        Parse(text).Match(_ => None, expression => Some(expression));

    public bool Matches(DateTimeOffset moment)
    {
        var utc = moment.ToUniversalTime();

        return Minute.Contains(utc.Minute)
               && Hour.Contains(utc.Hour)
               && Month.Contains(utc.Month)
               && MatchesDay(utc);
    }

    public Option<DateTimeOffset> Next(DateTimeOffset after)
    {
        var start = Truncate(after.ToUniversalTime()).AddMinutes(1);
        var limit = start.AddYears(SearchYears);

        for (var day = new DateTimeOffset(start.Year, start.Month, start.Day, 0, 0, 0, TimeSpan.Zero);
             day < limit;
             day = NextDay(day))
        {
            if (!Month.Contains(day.Month))
            {
                day = new DateTimeOffset(day.Year, day.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1).AddDays(-1);
                continue;
            }

            if (!MatchesDay(day))
            {
                continue;
            }

            foreach (var hour in Hour.Values)
            {
                foreach (var minute in Minute.Values)
                {
                    var candidate = day.AddHours(hour).AddMinutes(minute);

                    if (candidate >= start)
                    {
                        return Some(candidate);
                    }
                }
            }
        }

        return None;
    }

    public IReadOnlyList<DateTimeOffset> Occurrences(DateTimeOffset after, int count)
    {
        var result = new List<DateTimeOffset>();
        var cursor = after;

        while (result.Count < count)
        {
            var next = Next(cursor);

            if (next.IsNone())
            {
                break;
            }

            cursor = next.GetOrElse(cursor);
            result.Add(cursor);
        }

        return result;
    }

    public bool Equals(CronExpression? other) => other is not null && Text == other.Text;

    public override bool Equals(object? obj) => obj is CronExpression other && Equals(other);

    public override int GetHashCode() => Text.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Text;

    public static DateTimeOffset Truncate(DateTimeOffset moment) =>
        new(moment.Year, moment.Month, moment.Day, moment.Hour, moment.Minute, 0, moment.Offset);

    private static DateTimeOffset NextDay(DateTimeOffset day) => day.AddDays(1);

    private bool MatchesDay(DateTimeOffset moment)
    {
        var dayOfMonth = DayOfMonth.Contains(moment.Day);
        var dayOfWeek = DayOfWeek.Contains((int)moment.DayOfWeek);

        return DayOfMonth.IsWildcard || DayOfWeek.IsWildcard
            ? dayOfMonth && dayOfWeek
            : dayOfMonth || dayOfWeek;
    }
}
