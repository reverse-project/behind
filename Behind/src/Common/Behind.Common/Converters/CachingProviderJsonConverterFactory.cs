using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Behind.Common.Configuration;

namespace Behind.Common.Converters;

public sealed class CachingProviderJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(CachingProvider);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        new CachingProviderJsonConverter();

    private sealed class CachingProviderJsonConverter : JsonConverter<CachingProvider>
    {
        public override CachingProvider? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Caching provider must be a string.");
            }

            try
            {
                return (CachingProvider?)TypeDescriptor.GetConverter(typeof(CachingProvider))
                    .ConvertFromInvariantString(reader.GetString()!);
            }
            catch (Exception exception) when (exception is FormatException or NotSupportedException)
            {
                throw new JsonException("Invalid caching provider.", exception);
            }
        }

        public override void Write(Utf8JsonWriter writer, CachingProvider value, JsonSerializerOptions options)
        {
            if (value is not Memory and not RedisCompatible)
            {
                throw new JsonException("Unsupported caching provider.");
            }

            writer.WriteStringValue(value.Value);
        }
    }
}
