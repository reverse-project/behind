using System.Text.Json.Nodes;

namespace Behind.Common.Configuration;

public static class ConfigurationSchema
{
    public const string Name = "configuration-1.3.0";

    private static readonly Lazy<JsonObject> Definition = new(() => JsonSchemas.Describe(Name));

    public static JsonObject Describe() => JsonSchemas.Describe(Name);

    public static bool IsValid(JsonNode? value) =>
        TryNormalize(value, Definition.Value, out var normalized) && JsonSchemas.IsValid(Name, normalized);

    public static void Validate(JsonNode? value)
    {
        if (!IsValid(value))
        {
            throw new ConfigurationSchemaException();
        }
    }

    public static JsonObject PrepareForWrite(JsonObject value)
    {
        Validate(value);
        TryNormalize(value, Definition.Value, out var normalized, true);
        return normalized!.AsObject();
    }

    private static bool TryNormalize(JsonNode? value, JsonObject schema, out JsonNode? normalized, bool forWrite = false)
    {
        if (schema["$ref"] is JsonValue reference)
        {
            var definitionName = reference.GetValue<string>().Split('/')[^1];
            schema = Definition.Value["$defs"]![definitionName]!.AsObject();
            if (!forWrite && definitionName == "CachingProvider" && value is JsonValue provider
                && provider.TryGetValue<string>(out var text))
            {
                var canonical = schema["enum"]!.AsArray()
                    .Select(item => item!.GetValue<string>())
                    .FirstOrDefault(candidate => string.Equals(candidate, text, StringComparison.OrdinalIgnoreCase));
                normalized = JsonValue.Create(canonical ?? text);
                return true;
            }
        }

        if (schema["type"] is JsonValue type && type.TryGetValue<string>(out var kind) && kind == "integer")
        {
            return value.TryNormalizeInt64(out normalized);
        }

        if (value is JsonObject source)
        {
            var result = new JsonObject();
            var properties = Properties(schema).ToDictionary(property => property.Key, property => property.Value);
            foreach (var (name, child) in source)
            {
                var canonical = properties.Keys
                    .FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) ?? name;
                var childSchema = properties.GetValueOrDefault(canonical) as JsonObject
                    ?? schema["additionalProperties"] as JsonObject;
                JsonNode? replacement;
                if (childSchema is not null)
                {
                    if (!TryNormalize(child, childSchema, out replacement, forWrite))
                    {
                        normalized = null;
                        return false;
                    }
                }
                else
                {
                    replacement = child?.DeepClone();
                }

                if (!result.TryAdd(forWrite ? name : canonical, replacement))
                {
                    normalized = null;
                    return false;
                }
            }

            normalized = result;
            return true;
        }

        if (value is JsonArray array && schema["items"] is JsonObject itemSchema)
        {
            var result = new JsonArray();
            foreach (var item in array)
            {
                if (!TryNormalize(item, itemSchema, out var replacement, forWrite))
                {
                    normalized = null;
                    return false;
                }

                result.Add(replacement);
            }

            normalized = result;
            return true;
        }

        normalized = value?.DeepClone();
        return true;
    }

    private static IEnumerable<KeyValuePair<string, JsonNode?>> Properties(JsonObject schema)
    {
        if (schema["$ref"] is JsonValue reference)
        {
            return Properties(Definition.Value["$defs"]![reference.GetValue<string>().Split('/')[^1]]!.AsObject());
        }

        return (schema["properties"] as JsonObject ?? [])
            .Concat((schema["allOf"] as JsonArray ?? []).OfType<JsonObject>().SelectMany(Properties));
    }
}

public sealed class ConfigurationSchemaException() : Exception("Configuration does not match its JSON schema.");
