using Behind.Core.Common.Email;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Behind.Core.Common.Converters;

public sealed class EmailAddressJsonConverter : JsonConverter<EmailAddress>
{
    public static readonly EmailAddressTypeConverter TypeConverter = new();

    public override EmailAddress? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
        
        return (EmailAddress?)TypeConverter.ConvertFromInvariantString(stringValue);
    }

    public override void Write(Utf8JsonWriter writer, EmailAddress value, JsonSerializerOptions options) => writer.WriteStringValue(value.Address);
}