using System.Text.Json;
using System.Text.Json.Serialization;
using Behind.Core.Common.Configuration;

namespace Behind.Core.Common.Converters;

public sealed class EmailConfigurationJsonConverter : JsonConverter<EmailConfiguration>
{
    public override EmailConfiguration? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var jsonObject = jsonDoc.RootElement;
        var type = jsonObject.GetProperty("Provider").GetString();

        return type switch
        {
            "Smtp" => JsonSerializer.Deserialize<SmtpEmailConfiguration>(jsonObject.GetRawText(), options),
            "Logger" => JsonSerializer.Deserialize<LoggerEmailConfiguration>(jsonObject.GetRawText(), options),
            _ => throw new NotSupportedException($"Unsupported email configuration type: {type}")
        };
    }

    public override void Write(Utf8JsonWriter writer, EmailConfiguration value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case SmtpEmailConfiguration smtp:
                JsonSerializer.Serialize(writer, smtp, options);
                break;
            case LoggerEmailConfiguration logger:
                JsonSerializer.Serialize(writer, logger, options);
                break;
            default:
                throw new NotSupportedException($"Unsupported email configuration type: {value.Provider}");
        }
    }
}