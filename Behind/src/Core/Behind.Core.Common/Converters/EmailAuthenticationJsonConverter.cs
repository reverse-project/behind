using System.Text.Json.Serialization;
using System.Text.Json;
using Behind.Core.Common.Configuration;
namespace Behind.Core.Common.Converters;

public sealed class EmailAuthenticationJsonConverter : JsonConverter<Authentication>
{
    private static readonly EmailAuthenticationTypeConverter TypeConverter = new();

    public override Authentication? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return null;
        }

        var stringValue = reader.GetString();

        if (stringValue is null)
        {
            return null;
        }

        return (Authentication?)TypeConverter.ConvertFrom(stringValue);
    }

    public override void Write(Utf8JsonWriter writer, Authentication value, JsonSerializerOptions options)
    {
        var stringValue = TypeConverter.ConvertTo(value, typeof(string)) as string;
        writer.WriteStringValue(stringValue);
    }
}