using System.Text.Json;
using System.Text.Json.Serialization;

namespace Behind.Core.Domain.Converters;

public sealed class ScheduleParametersJsonConverter : JsonConverter<ScheduleParameters>
{
    public override ScheduleParameters Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);

        return ScheduleExtensions.ParseScheduleParameters(document.RootElement.GetRawText());
    }

    public override void Write(Utf8JsonWriter writer, ScheduleParameters value, JsonSerializerOptions options)
    {
        using var document = JsonDocument.Parse(value.ToJson());
        document.RootElement.WriteTo(writer);
    }
}
