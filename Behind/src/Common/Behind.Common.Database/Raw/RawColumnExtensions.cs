using System.Globalization;
using System.Text.Json;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Database.Raw;

public static class RawColumnExtensions
{
    extension(RawColumn column)
    {
        public bool IsSearchable => column.Kind != RawValueKind.Binary && !column.IsSensitive;

        public object? Normalize(object? stored) =>
            stored switch
            {
                null or DBNull => null,
                byte[] bytes => Convert.ToBase64String(bytes),
                Guid guid => guid.ToString(),
                string text when column.Kind == RawValueKind.Guid && Guid.TryParse(text, out var guid) =>
                    guid.ToString(),
                _ when column.Kind == RawValueKind.Boolean => Convert.ToBoolean(stored, CultureInfo.InvariantCulture),
                _ => stored
            };

        public object? Present(object? normalized) =>
            column.IsSensitive && normalized is not null ? SensibleExtensions.MaskedValue : normalized;

        public bool IsMasked(JsonElement element) =>
            column.IsSensitive
            && element.ValueKind == JsonValueKind.String
            && element.GetString() == SensibleExtensions.MaskedValue;

        public Validation<RawValue> Parse(JsonElement element) =>
            column.ToStoredValue(element).Match(
                () => Invalid<RawValue>(Error($"Column {column.Name} expects a {column.Kind} value.")),
                value => Valid(new RawValue(column, value)));

        private Option<object> ToStoredValue(JsonElement element) =>
            element.ValueKind == JsonValueKind.Null
                ? Some<object>(DBNull.Value)
                : column.Kind switch
                {
                    RawValueKind.Text => Some<object>(
                        element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText()),
                    RawValueKind.Integer => element.ToInteger(),
                    RawValueKind.Number => element.ToNumber(),
                    RawValueKind.Boolean => element.ToBoolean(),
                    RawValueKind.Binary => element.ToText().Bind(text => text.ToBytes()),
                    RawValueKind.Guid => element.ToText().Bind(text => Parsed(Guid.TryParse(text, out var guid), guid)),
                    _ => element.ToText().Bind(text => Parsed(
                        DateTimeOffset.TryParse(
                            text,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal,
                            out var moment),
                        moment))
                };
    }

    extension(JsonElement element)
    {
        private Option<string> ToText() =>
            element.ValueKind == JsonValueKind.String ? Some(element.GetString()!) : None;

        private Option<object> ToInteger() =>
            element.ValueKind switch
            {
                JsonValueKind.Number => Parsed(element.TryGetInt64(out var number), number),
                JsonValueKind.String => Parsed(
                    long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var text),
                    text),
                _ => None
            };

        private Option<object> ToNumber() =>
            element.ValueKind switch
            {
                JsonValueKind.Number => Parsed(element.TryGetDouble(out var number), number),
                JsonValueKind.String => Parsed(
                    double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var text),
                    text),
                _ => None
            };

        private Option<object> ToBoolean() =>
            element.ValueKind switch
            {
                JsonValueKind.True => Some<object>(true),
                JsonValueKind.False => Some<object>(false),
                JsonValueKind.Number => Parsed(element.TryGetInt64(out var number), number != 0),
                JsonValueKind.String => Parsed(bool.TryParse(element.GetString(), out var text), text),
                _ => None
            };
    }

    extension(string text)
    {
        private Option<object> ToBytes() =>
            text.Length == 0 ? Some<object>(Array.Empty<byte>()) : text.FromBase64().Map(bytes => (object)bytes);
    }

    private static Option<object> Parsed<T>(bool parsed, T value) where T : notnull =>
        parsed ? Some<object>(value) : None;
}
