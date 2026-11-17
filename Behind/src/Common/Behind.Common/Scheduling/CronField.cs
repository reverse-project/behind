using System.Collections.Immutable;
using System.Globalization;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Scheduling;

public sealed record CronFieldSpecification(string Name, int Minimum, int Maximum, IReadOnlyList<string> Names)
{
    public static readonly CronFieldSpecification Minute = new(nameof(Minute), 0, 59, []);
    public static readonly CronFieldSpecification Hour = new(nameof(Hour), 0, 23, []);
    public static readonly CronFieldSpecification DayOfMonth = new(nameof(DayOfMonth), 1, 31, []);

    public static readonly CronFieldSpecification Month = new(
        nameof(Month), 1, 12, ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"]);

    public static readonly CronFieldSpecification DayOfWeek = new(
        nameof(DayOfWeek), 0, 7, ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"]);

    public static readonly IReadOnlyList<CronFieldSpecification> All = [Minute, Hour, DayOfMonth, Month, DayOfWeek];

    public int WildcardMaximum => this == DayOfWeek ? 6 : Maximum;
}

public sealed class CronField : IEquatable<CronField>
{
    private const char ListSeparator = ',';
    private const char RangeSeparator = '-';
    private const char StepSeparator = '/';
    private const string Wildcard = "*";
    private const string AnyValue = "?";
    private const int Sunday = 0;
    private const int AlternativeSunday = 7;

    private CronField(string text, ImmutableSortedSet<int> values, bool isWildcard)
    {
        Text = text;
        Values = values;
        IsWildcard = isWildcard;
    }

    public string Text { get; }

    public ImmutableSortedSet<int> Values { get; }

    public bool IsWildcard { get; }

    public bool Contains(int value) => Values.Contains(value);

    public static Validation<CronField> Parse(string text, CronFieldSpecification specification) =>
        text.Split(ListSeparator)
            .Select(part => ParsePart(part, specification))
            .Aggregate(
                Valid(ImmutableSortedSet<int>.Empty),
                (accumulated, part) => accumulated.Match(
                    Invalid<ImmutableSortedSet<int>>,
                    values => part.Match(
                        Invalid<ImmutableSortedSet<int>>,
                        partValues => Valid(values.Union(partValues)))))
            .Match(
                Invalid<CronField>,
                values => Valid(new CronField(
                    text,
                    Normalize(values, specification),
                    IsWildcardText(text))));

    public bool Equals(CronField? other) => other is not null && Text == other.Text;

    public override bool Equals(object? obj) => obj is CronField other && Equals(other);

    public override int GetHashCode() => Text.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Text;

    // Like cronie, a field starting with * or ? (including */N) stays a wildcard for day matching.
    private static bool IsWildcardText(string text) =>
        text.StartsWith(Wildcard, StringComparison.Ordinal) || text.StartsWith(AnyValue, StringComparison.Ordinal);

    private static ImmutableSortedSet<int> Normalize(ImmutableSortedSet<int> values, CronFieldSpecification specification) =>
        specification == CronFieldSpecification.DayOfWeek && values.Contains(AlternativeSunday)
            ? values.Remove(AlternativeSunday).Add(Sunday)
            : values;

    private static Validation<ImmutableSortedSet<int>> ParsePart(string part, CronFieldSpecification specification)
    {
        var segments = part.Split(StepSeparator);

        if (part.Length == 0 || segments.Length > 2)
        {
            return Invalid<ImmutableSortedSet<int>>(Error($"{specification.Name}: invalid value '{part}'"));
        }

        var step = segments.Length == 2 ? ParseNumber(segments[1]) : Some(1);

        return step.Where(value => value > 0).Match(
            () => Invalid<ImmutableSortedSet<int>>(Error($"{specification.Name}: invalid step '{part}'")),
            stepValue => ParseRange(segments[0], segments.Length == 2, specification).Match(
                Invalid<ImmutableSortedSet<int>>,
                range => Valid(Enumerable.Range(range.Start, range.End - range.Start + 1)
                    .Where((_, index) => index % stepValue == 0)
                    .ToImmutableSortedSet())));
    }

    private static Validation<(int Start, int End)> ParseRange(
        string range,
        bool stepped,
        CronFieldSpecification specification)
    {
        if (range is Wildcard or AnyValue)
        {
            return Valid((specification.Minimum, specification.WildcardMaximum));
        }

        var bounds = range.Split(RangeSeparator);

        if (bounds.Length > 2)
        {
            return Invalid<(int, int)>(Error($"{specification.Name}: invalid range '{range}'"));
        }

        var start = ParseValue(bounds[0], specification);
        var end = bounds.Length == 2
            ? ParseValue(bounds[1], specification)
            : stepped
                ? start.Map(_ => specification.Maximum)
                : start;

        return start.Match(
            () => Invalid<(int, int)>(OutOfRange(bounds[0], specification)),
            first => end.Match(
                () => Invalid<(int, int)>(OutOfRange(bounds[^1], specification)),
                last => last >= first
                    ? Valid((first, last))
                    : Invalid<(int, int)>(Error($"{specification.Name}: range '{range}' is reversed"))));
    }

    private static Error OutOfRange(string value, CronFieldSpecification specification) =>
        Error($"{specification.Name}: '{value}' is out of range {specification.Minimum}-{specification.Maximum}");

    private static Option<int> ParseValue(string value, CronFieldSpecification specification)
    {
        var named = specification.Names
            .Select((name, position) => (name, position))
            .Where(entry => string.Equals(entry.name, value, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.position + specification.Minimum)
            .ToList();

        return named.Count > 0
            ? Some(named[0])
            : ParseNumber(value).Where(number => number >= specification.Minimum && number <= specification.Maximum);
    }

    private static Option<int> ParseNumber(string value) =>
        value.Length > 0
        && value.All(char.IsAsciiDigit)
        && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? Some(number)
            : None;
}
