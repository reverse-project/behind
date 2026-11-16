using System.Text.Json;
using System.Text.Json.Serialization;

namespace Behind.Common.Templating;

public sealed class TemplatesJsonConverter : JsonConverter<Templates>
{
    public override Templates Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(ref reader, options) ?? [];

        return (Templates)new TemplatesTypeConverter().ConvertFrom(values)!;
    }

    public override void Write(Utf8JsonWriter writer, Templates value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var template in value)
        {
            writer.WriteString(template.Name, template.Content);
        }

        writer.WriteEndObject();
    }
}
