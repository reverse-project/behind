using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Behind.Contact.Domain;

[TypeConverter(typeof(ContactStringEnumTypeConverter<ContactSubjectMode>))]
[JsonConverter(typeof(ContactStringEnumJsonConverter<ContactSubjectMode>))]
public enum ContactSubjectMode
{
    [EnumMember(Value = "freeText")]
    FreeText,
    [EnumMember(Value = "choices")]
    Choices
}

[TypeConverter(typeof(ContactStringEnumTypeConverter<ContactFieldType>))]
[JsonConverter(typeof(ContactStringEnumJsonConverter<ContactFieldType>))]
public enum ContactFieldType
{
    [EnumMember(Value = "text")]
    Text,
    [EnumMember(Value = "textarea")]
    Textarea,
    [EnumMember(Value = "email")]
    Email,
    [EnumMember(Value = "boolean")]
    Boolean
}

[TypeConverter(typeof(ContactStringEnumTypeConverter<ContactDeliveryStatus>))]
[JsonConverter(typeof(ContactStringEnumJsonConverter<ContactDeliveryStatus>))]
public enum ContactDeliveryStatus
{
    [EnumMember(Value = "notRequested")]
    NotRequested,
    [EnumMember(Value = "sending")]
    Sending,
    [EnumMember(Value = "sent")]
    Sent,
    [EnumMember(Value = "failed")]
    Failed
}

public static class ContactStringEnum
{
    public static string ToToken<TEnum>(TEnum value) where TEnum : struct, Enum =>
        Enum.GetName(value) is { } name
            ? typeof(TEnum).GetField(name)!.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? name
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown {typeof(TEnum).Name} value.");

    public static TEnum Parse<TEnum>(string token) where TEnum : struct, Enum =>
        TryParse(token, out TEnum value)
            ? value
            : throw new FormatException($"Unknown {typeof(TEnum).Name} value '{token}'.");

    public static bool TryParse<TEnum>(string? token, out TEnum value) where TEnum : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(ToToken(candidate), token, StringComparison.Ordinal))
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }
}

public sealed class ContactStringEnumTypeConverter<TEnum> : TypeConverter where TEnum : struct, Enum
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value) =>
        value is string token
            ? ContactStringEnum.Parse<TEnum>(token)
            : base.ConvertFrom(context, culture, value);

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        System.Globalization.CultureInfo? culture,
        object? value,
        Type destinationType) =>
        destinationType == typeof(string) && value is TEnum enumValue
            ? ContactStringEnum.ToToken(enumValue)
            : base.ConvertTo(context, culture, value, destinationType);
}

public sealed class ContactStringEnumJsonConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String
            || !ContactStringEnum.TryParse(reader.GetString(), out TEnum value))
        {
            throw new JsonException($"Expected a valid {typeof(TEnum).Name} string.");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ContactStringEnum.ToToken(value));
}
