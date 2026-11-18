using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Behind.Common.Configuration;

namespace Behind.Common.Converters;

public sealed class CachingKeyJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(CachingKey);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        new CachingKeyJsonConverter();

    private sealed class CachingKeyJsonConverter : JsonConverter<CachingKey>
    {
        public override CachingKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Caching key must be a string.");
            }

            return (CachingKey?)TypeDescriptor.GetConverter(typeof(CachingKey))
                .ConvertFromInvariantString(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, CachingKey value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
