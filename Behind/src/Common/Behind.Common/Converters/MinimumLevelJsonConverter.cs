using System.Text.Json;
using System.Text.Json.Serialization;
using Behind.Common.Logging;
using Serilog.Events;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Converters;

public sealed class MinimumLevelJsonConverter : JsonConverter<MinimumLevelConfiguration>
{
    private const string DefaultProperty = "Default";
    private const string OverrideProperty = "Override";

    public override MinimumLevelConfiguration Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return MinimumLevelConfiguration.Empty;
        }

        var root = document.RootElement;

        return new MinimumLevelConfiguration(
            root.TryGetProperty(DefaultProperty, out var level) ? level.ToLevel() : None,
            root.TryGetProperty(OverrideProperty, out var overrides) && overrides.ValueKind == JsonValueKind.Object
                ? overrides.EnumerateObject()
                    .Select(property => (property.Name, Level: property.Value.ToLevel()))
                    .SelectMany(pair => pair.Level.AsEnumerable().Select(found => (pair.Name, Level: found)))
                    .ToDictionary(pair => pair.Name, pair => pair.Level)
                : new Dictionary<string, LogEventLevel>());
    }

    public override void Write(Utf8JsonWriter writer, MinimumLevelConfiguration value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var level in value.Default.AsEnumerable())
        {
            writer.WriteString(DefaultProperty, level.ToString());
        }

        if (value.Overrides.Count > 0)
        {
            writer.WriteStartObject(OverrideProperty);

            foreach (var (source, level) in value.Overrides)
            {
                writer.WriteString(source, level.ToString());
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}
