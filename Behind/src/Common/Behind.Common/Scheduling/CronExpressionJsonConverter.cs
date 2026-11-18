using System.Text.Json;
using System.Text.Json.Serialization;

namespace Behind.Common.Scheduling;

public sealed class CronExpressionJsonConverter : JsonConverter<CronExpression>
{
    public override CronExpression Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Cron expression must be a string.");
        }

        return CronExpression.Parse(reader.GetString()).Match(
            errors => throw new JsonException(string.Join("; ", errors.Select(error => error.Message))),
            expression => expression);
    }

    public override void Write(Utf8JsonWriter writer, CronExpression value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Text);
}
