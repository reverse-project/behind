namespace Behind.Common.Converters;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class SensibleJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                   .Cast<MemberInfo>()
                   .Concat(typeToConvert.GetFields(BindingFlags.Public | BindingFlags.Instance)
                       .Cast<MemberInfo>())
                   .Any(member => member.GetCustomAttribute<SensibleAttribute>() is not null);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(SensibleJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

internal sealed class SensibleJsonConverter<T> : JsonConverter<T> where T : notnull
{
    private static readonly Dictionary<string, MemberInfo> SensibleMembers = [];
    private static readonly JsonConverter<T> DefaultConverter = new DefaultJsonConverter<T>();

    static SensibleJsonConverter()
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (prop.GetCustomAttribute<SensibleAttribute>() is not null)
            {
                SensibleMembers[prop.Name] = prop;
            }
        }

        foreach (var field in fields)
        {
            if (field.GetCustomAttribute<SensibleAttribute>() is not null)
            {
                SensibleMembers[field.Name] = field;
            }
        }
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<T>(ref reader, options) ?? throw new JsonException("Deserialization resulted in null");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        SensitiveContext.VerifyContextActive();

        if (value is JsonElement element)
        {
            element.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            {
                continue;
            }

            var value_property = prop.GetValue(value);
            var propertyName = options.PropertyNamingPolicy?.ConvertName(prop.Name) ?? prop.Name;

            if (SensibleMembers.ContainsKey(prop.Name) && value_property is string stringValue)
            {
                writer.WritePropertyName(propertyName);
                writer.WriteStringValue(stringValue.MaskSensitiveValue());
            }
            else
            {
                writer.WritePropertyName(propertyName);
                JsonSerializer.Serialize(writer, value_property, value_property?.GetType() ?? prop.PropertyType, options);
            }
        }

        foreach (var field in fields)
        {
            if (field.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            {
                continue;
            }

            var value_field = field.GetValue(value);
            var fieldName = options.PropertyNamingPolicy?.ConvertName(field.Name) ?? field.Name;

            if (SensibleMembers.ContainsKey(field.Name) && value_field is string stringValue)
            {
                writer.WritePropertyName(fieldName);
                writer.WriteStringValue(stringValue.MaskSensitiveValue());
            }
            else
            {
                writer.WritePropertyName(fieldName);
                JsonSerializer.Serialize(writer, value_field, value_field?.GetType() ?? field.FieldType, options);
            }
        }

        writer.WriteEndObject();
    }
}

internal sealed class DefaultJsonConverter<T> : JsonConverter<T> where T : notnull
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<T>(ref reader, options) ?? throw new JsonException("Deserialization resulted in null");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, typeof(T), options);
    }
}
