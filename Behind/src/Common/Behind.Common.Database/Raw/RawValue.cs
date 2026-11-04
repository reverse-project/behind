namespace Behind.Common.Database.Raw;

public sealed record RawValue(RawColumn Column, object Value);

public static class RawValueListExtensions
{
    extension(IEnumerable<RawValue> values)
    {
        public string AuditKey => string.Join(':', values.Select(value => value.Column.Normalize(value.Value)));
    }
}
