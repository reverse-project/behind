using System.Text.Json;
using System.Text.Json.Serialization;

namespace Behind.Core.Domain.Converters;

public sealed class ScheduleParticipantsJsonConverter : JsonConverter<ScheduleParticipants>
{
    public override ScheduleParticipants Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);

        try
        {
            return ScheduleExtensions.ParseScheduleParticipants(document.RootElement.GetRawText());
        }
        catch (FormatException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, ScheduleParticipants value, JsonSerializerOptions options)
    {
        using var document = JsonDocument.Parse(value.ToJson());
        document.RootElement.WriteTo(writer);
    }
}
