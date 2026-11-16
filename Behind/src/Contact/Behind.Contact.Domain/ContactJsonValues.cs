using System.Collections.Immutable;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Behind.Common.Configuration;

namespace Behind.Contact.Domain;

public interface IContactJsonArray<TSelf, TItem>
    where TSelf : class, IContactJsonArray<TSelf, TItem>
{
    ImmutableArray<TItem> Items { get; }

    static abstract TSelf Create(ImmutableArray<TItem> items);
}

[TypeConverter(typeof(ContactJsonArrayTypeConverter<ContactSubjects, string>))]
[JsonConverter(typeof(ContactJsonArrayConverterFactory))]
public sealed record ContactSubjects(ImmutableArray<string> Items) : IContactJsonArray<ContactSubjects, string>
{
    public static ContactSubjects Create(ImmutableArray<string> items) => new(items);

    public static ContactSubjects From(IEnumerable<string> items) => new(items.ToImmutableArray());
}

[TypeConverter(typeof(ContactJsonArrayTypeConverter<ContactFieldSchema, ContactField>))]
[JsonConverter(typeof(ContactJsonArrayConverterFactory))]
public sealed record ContactFieldSchema(ImmutableArray<ContactField> Items) : IContactJsonArray<ContactFieldSchema, ContactField>
{
    public static ContactFieldSchema Create(ImmutableArray<ContactField> items) => new(items);

    public static ContactFieldSchema From(IEnumerable<ContactField> items) => new(items.ToImmutableArray());
}

[TypeConverter(typeof(ContactJsonArrayTypeConverter<ContactFieldValues, ContactValue>))]
[JsonConverter(typeof(ContactJsonArrayConverterFactory))]
public sealed record ContactFieldValues(ImmutableArray<ContactValue> Items) : IContactJsonArray<ContactFieldValues, ContactValue>
{
    public static ContactFieldValues Create(ImmutableArray<ContactValue> items) => new(items);

    public static ContactFieldValues From(IEnumerable<ContactValue> items) => new(items.ToImmutableArray());

    public static ContactFieldValues Empty { get; } = new([]);
}

public sealed class ContactJsonArrayConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.GetInterfaces().Any(interfaceType =>
            interfaceType.IsGenericType
            && interfaceType.GetGenericTypeDefinition() == typeof(IContactJsonArray<,>));

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var contract = typeToConvert.GetInterfaces().SingleOrDefault(interfaceType =>
            interfaceType.IsGenericType
            && interfaceType.GetGenericTypeDefinition() == typeof(IContactJsonArray<,>))
            ?? throw new InvalidOperationException($"{typeToConvert.Name} is not a contact JSON array value.");
        var itemType = contract.GetGenericArguments()[1];
        var converterType = typeof(ContactJsonArrayConverter<,>).MakeGenericType(typeToConvert, itemType);
        return (JsonConverter)(Activator.CreateInstance(converterType)
            ?? throw new InvalidOperationException($"Could not create a JSON converter for {typeToConvert.Name}."));
    }

    private sealed class ContactJsonArrayConverter<TValue, TItem> : JsonConverter<TValue>
        where TValue : class, IContactJsonArray<TValue, TItem>
    {
        private static string SchemaName => typeof(TValue) == typeof(ContactSubjects)
            ? "contact-subjects-1.0.0"
            : typeof(TValue) == typeof(ContactFieldSchema)
                ? "contact-fields-1.0.0"
                : typeof(TValue) == typeof(ContactFieldValues)
                    ? "contact-values-1.0.0"
                    : throw new InvalidOperationException($"No JSON schema for {typeof(TValue).Name}.");

        public override TValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Contact values must be JSON arrays.");
            }

            var json = JsonNode.Parse(ref reader);
            JsonSchemas.Validate(SchemaName, json);
            var items = json.Deserialize<ImmutableArray<TItem>>(options);
            return TValue.Create(items);
        }

        public override void Write(Utf8JsonWriter writer, TValue value, JsonSerializerOptions options)
        {
            var json = JsonSerializer.SerializeToNode(value.Items, options);
            JsonSchemas.Validate(SchemaName, json);
            json!.WriteTo(writer, options);
        }
    }
}

public sealed class ContactJsonArrayTypeConverter<TValue, TItem> : TypeConverter
    where TValue : class, IContactJsonArray<TValue, TItem>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
    {
        if (value is not string json)
        {
            return base.ConvertFrom(context, culture, value);
        }

        return JsonSerializer.Deserialize<TValue>(json, JsonOptions)
            ?? throw new JsonException("Contact values must not be null.");
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        System.Globalization.CultureInfo? culture,
        object? value,
        Type destinationType)
    {
        if (destinationType == typeof(string) && value is TValue contactValue)
        {
            return JsonSerializer.Serialize(contactValue, JsonOptions);
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
