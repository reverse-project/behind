using Behind.Core.Common.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Behind.Core.Common.Converters;
public sealed class SecretKeyJsonConverter : JsonConverter<SecretKey>
{
    private static readonly SecretKeyTypeConverter TypeConverter = new();

    public override SecretKey? Read(
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
            throw new JsonException("Secret key must be a string.");
        }

        string? value = reader.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Secret key cannot be empty.");
        }

        try
        {
            return (SecretKey?)TypeConverter.ConvertFromInvariantString(value);
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            throw new JsonException("Invalid secret key format.", exception);
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        SecretKey value,
        JsonSerializerOptions options)
    {
        string serialized = value switch
        {
            Totp totp => $"totp://{totp.Value}",
            _ => throw new JsonException($"Unsupported secret key type '{value.GetType().Name}'.")
        };

        writer.WriteStringValue(serialized);
    }
}