using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Behind.Core.Common.Configuration;

namespace Behind.Core.Common.Converters;

public sealed class ProviderJsonConverter : JsonConverter<Provider>
{
    private static readonly ProviderTypeConverter TypeConverter = new();

    public override Provider? Read(
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
            throw new JsonException("Provider must be a string.");
        }

        string? value = reader.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Provider cannot be empty.");
        }

        try
        {
            return (Provider?)TypeConverter.ConvertFromInvariantString(value);
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            throw new JsonException("Invalid provider format.", exception);
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        Provider value,
        JsonSerializerOptions options)
    {
        string serialized = value switch
        {
            Sqlite or Postgres => value.Value,
            _ => throw new JsonException($"Unsupported provider type '{value.GetType().Name}'.")
        };

        writer.WriteStringValue(serialized);
    }
}
