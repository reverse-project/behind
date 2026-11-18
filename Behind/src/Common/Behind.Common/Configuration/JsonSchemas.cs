using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Behind.Common.Configuration;

public static class JsonSchemas
{
    private static readonly ConcurrentDictionary<string, Lazy<JsonSchema>> Validators = new(StringComparer.Ordinal);

    public static JsonObject Describe(string name)
    {
        var resource = $"Behind.Common.Configuration.Schemas.{name}.schema.json";
        using var stream = typeof(JsonSchemas).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"JSON schema resource '{resource}' is missing.");
        return JsonNode.Parse(stream)!.AsObject();
    }

    public static bool IsValid(string name, JsonNode? value) =>
        Validators.GetOrAdd(name, key => new Lazy<JsonSchema>(
            () => JsonSchema.FromText(Describe(key).ToJsonString()))).Value.Evaluate(value).IsValid;

    public static void Validate(string name, JsonNode? value)
    {
        if (!IsValid(name, value))
        {
            throw new JsonException($"JSON does not match schema '{name}'.");
        }
    }
}
