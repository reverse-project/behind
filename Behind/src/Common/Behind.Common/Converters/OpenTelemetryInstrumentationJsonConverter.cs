using System.Text.Json;
using System.Text.Json.Serialization;
using Instrumentation = Behind.Common.Configuration.OpenTelemetryConfiguration.Instrumentation;

namespace Behind.Common.Converters;

public sealed class OpenTelemetryInstrumentationJsonConverter : JsonConverter<Instrumentation>
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

        var name = reader.GetString();
        if (name is null)
        {
            throw new JsonException("OpenTelemetry instrumentation cannot be null.");
        }

        try
        {
            return (Instrumentation)OpenTelemetryInstrumentationTypeConverter.Instance
                .ConvertFromInvariantString(name)!;
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
        var name = value switch
        {
            var instrumentation when instrumentation == Instrumentation.AspNetCoreInstrumentation =>
                nameof(Instrumentation.AspNetCoreInstrumentation),
            var instrumentation when instrumentation == Instrumentation.HttpClientInstrumentation =>
                nameof(Instrumentation.HttpClientInstrumentation),
            var instrumentation when instrumentation == Instrumentation.RuntimeInstrumentation =>
                nameof(Instrumentation.RuntimeInstrumentation),
            var instrumentation when instrumentation == Instrumentation.OtlpExporter =>
                nameof(Instrumentation.OtlpExporter),
            _ => throw new JsonException($"Unknown OpenTelemetry instrumentation '{value}'.")
        };

        writer.WriteStringValue(name);
    }
}
