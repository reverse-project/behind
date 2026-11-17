using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Behind.Core.Common.Configuration;

namespace Behind.Core.Common.Converters;

public sealed class DatabaseKeyJsonConverter : JsonConverter<DatabaseKey>
{
    private static readonly DatabaseKeyTypeConverter TypeConverter = new();

    public override DatabaseKey? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Key must be a string.");
        }

        string? value = reader.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Key cannot be empty.");
        }

        try
        {
            return (DatabaseKey?)TypeConverter.ConvertFromInvariantString(value);
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            throw new JsonException("Invalid key format.", exception);
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        DatabaseKey value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}