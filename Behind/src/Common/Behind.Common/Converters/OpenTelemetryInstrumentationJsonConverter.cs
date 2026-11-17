using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Instrumentation = Behind.Common.Configuration.OpenTelemetryConfiguration.Instrumentation;

namespace Behind.Common.Converters;

public sealed class OpenTelemetryInstrumentationJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Instrumentation);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        new InstrumentationJsonConverter(TypeDescriptor.GetConverter(typeof(Instrumentation)));

    private sealed class InstrumentationJsonConverter(TypeConverter converter) : JsonConverter<Instrumentation>
    {
        public override Instrumentation Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("OpenTelemetry instrumentation must be a string.");
            }

            var name = reader.GetString() ?? throw new JsonException("OpenTelemetry instrumentation cannot be null.");

            try
            {
                return (Instrumentation)converter.ConvertFrom(null, CultureInfo.InvariantCulture, name)!;
            }
            catch (FormatException exception)
            {
                throw new JsonException(exception.Message, exception);
            }
        }

        public override void Write(
            Utf8JsonWriter writer,
            Instrumentation value,
            JsonSerializerOptions options)
        {
            try
            {
                writer.WriteStringValue((string)converter.ConvertTo(null, CultureInfo.InvariantCulture, value, typeof(string))!);
            }
            catch (FormatException exception)
            {
                throw new JsonException(exception.Message, exception);
            }
        }
    }
}
