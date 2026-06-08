using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Behind.Core.Domain;

namespace Behind.Core.Database.Converters;

public sealed class AuditLogEntryAuditActionJsonConverter : JsonConverter<AuditLogEntry.AuditAction>
{
    public const string DiscriminatorPropertyName = "value";

    private static readonly Type BaseType = typeof(AuditLogEntry.AuditAction);

    private static readonly IReadOnlyDictionary<string, Type> ActionTypes =
        typeof(AuditLogEntry)
            .GetNestedTypes(BindingFlags.Public)
            .Where(type => type.IsAssignableTo(BaseType) && !type.IsAbstract)
            .ToDictionary(type => type.Name, StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<Type, bool> RequiresJsonCache = new();

    private static readonly JsonSerializerOptions PolymorphicOptions = CreatePolymorphicOptions();

    public override AuditLogEntry.AuditAction? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
            {
                string? actionName = reader.GetString();

                return string.IsNullOrWhiteSpace(actionName)
                    ? throw new JsonException("Audit action cannot be empty.")
                    : CreateSimpleAction(actionName);
            }
        }

        if (reader.TokenType is not JsonTokenType.StartObject)
        {
            throw new JsonException("Audit action must be either a string or a JSON object.");
        }

        return JsonSerializer.Deserialize<AuditLogEntry.AuditAction>(ref reader, PolymorphicOptions)
               ?? throw new JsonException("Could not deserialize audit action.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        AuditLogEntry.AuditAction value,
        JsonSerializerOptions options)
    {
        if (RequiresJson(value.GetType()))
        {
            JsonSerializer.Serialize(writer, value, PolymorphicOptions);
            return;
        }

        writer.WriteStringValue(value.GetType().Name);
    }

    public static AuditLogEntry.AuditAction FromStorageValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Audit action cannot be empty.");
        }

        return IsJson(value)
            ? JsonSerializer.Deserialize<AuditLogEntry.AuditAction>(value, PolymorphicOptions)
              ?? throw new JsonException("Could not deserialize audit action.")
            : CreateSimpleAction(value);
    }

    public static string ToStorageValue(AuditLogEntry.AuditAction action)
    {
        return RequiresJson(action.GetType())
            ? JsonSerializer.Serialize(action, PolymorphicOptions)
            : action.GetType().Name;
    }

    private static bool IsJson(string value) => value.TrimStart().StartsWith('{');

    private static AuditLogEntry.AuditAction CreateSimpleAction(string actionName)
    {
        if (!ActionTypes.TryGetValue(actionName, out Type? actionType))
        {
            throw new JsonException($"Unknown audit action '{actionName}'.");
        }

        if (RequiresJson(actionType))
        {
            throw new JsonException(
                $"Audit action '{actionName}' requires JSON because it contains payload properties.");
        }

        try
        {
            return (AuditLogEntry.AuditAction?)Activator.CreateInstance(actionType)
                   ?? throw new JsonException($"Could not create audit action '{actionName}'.");
        }
        catch (MissingMethodException exception)
        {
            throw new JsonException(
                $"Audit action '{actionName}' cannot be created from a plain string because it has no parameterless constructor.",
                exception);
        }
    }

    private static bool RequiresJson(Type actionType) =>
        RequiresJsonCache.GetOrAdd(actionType, static type =>
            GetSerializablePayloadProperties(type).Any());

    private static IEnumerable<PropertyInfo> GetSerializablePayloadProperties(Type type) =>
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Where(property => property.GetMethod is not null)
            .Where(property => property.GetCustomAttribute<JsonIgnoreAttribute>() is null);

    private static JsonSerializerOptions CreatePolymorphicOptions() =>
        new(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    ConfigureAuditActionPolymorphism
                }
            }
        };

    private static void ConfigureAuditActionPolymorphism(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != BaseType)
        {
            return;
        }

        JsonPolymorphismOptions polymorphismOptions = new()
        {
            TypeDiscriminatorPropertyName = DiscriminatorPropertyName,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
            IgnoreUnrecognizedTypeDiscriminators = false
        };

        foreach ((string actionName, Type actionType) in ActionTypes)
        {
            polymorphismOptions.DerivedTypes.Add(
                new JsonDerivedType(actionType, actionName));
        }

        typeInfo.PolymorphismOptions = polymorphismOptions;
    }
}