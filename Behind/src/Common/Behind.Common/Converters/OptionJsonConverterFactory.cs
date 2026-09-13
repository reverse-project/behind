using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Converters;

public sealed class OptionJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType &&
               typeToConvert.GetGenericTypeDefinition() == typeof(Option<>);
    }

    public override JsonConverter CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        Type innerType = typeToConvert.GetGenericArguments()[0];
        Type converterType = typeof(OptionJsonConverter<>).MakeGenericType(innerType);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class OptionJsonConverter<T> : JsonConverter<Option<T>>
    {
        public override Option<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType is JsonTokenType.Null)
            {
                return None;
            }

            if (reader.TokenType is JsonTokenType.String)
            {
                string? stringValue = reader.GetString();

                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    return None;
                }

                TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));

                if (converter.CanConvertFrom(typeof(string)))
                {
                    try
                    {
                        object? converted = converter.ConvertFrom(
                            null,
                            CultureInfo.InvariantCulture,
                            stringValue);

                        return converted is null ? None : Some((T)converted);
                    }
                    catch (Exception exception) when (exception is FormatException or NotSupportedException)
                    {
                        throw new JsonException(
                            $"Value '{stringValue}' cannot be converted to Option<{typeof(T).Name}>.",
                            exception);
                    }
                }
            }

            T? value = JsonSerializer.Deserialize<T>(ref reader, options);

            return value is null ? None : Some(value);
        }

        public override void Write(
            Utf8JsonWriter writer,
            Option<T> value,
            JsonSerializerOptions options)
        {
            value.Match(
                () =>
                {
                    writer.WriteNullValue();
                    return 0;
                },
                some =>
                {
                    JsonSerializer.Serialize(writer, some, options);
                    return 0;
                });
        }
    }
}