using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common;
using Behind.Common.Configuration;
using Behind.Common.Logging;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Settings;

public static class SerilogSettings
{
    private static readonly string[] Paths =
        ["Serilog:Using", "Serilog:Enrich", "Serilog:WriteTo", "Serilog:AuditTo", "Serilog:Filter", "Serilog:Destructure"];
    private static readonly Lazy<JsonObject> Defaults = new(() =>
    {
        using var stream = typeof(SerilogExtensions).Assembly.GetManifestResourceStream(SerilogExtensions.DefaultsResourceName)
            ?? throw new InvalidOperationException("The Serilog defaults resource is missing.");
        return JsonNode.Parse(stream)!.AsObject();
    });

    public static bool IsCollection(string file, string path) =>
        string.Equals(file, ConfigurationFileCatalog.AppSettingsFile, StringComparison.OrdinalIgnoreCase)
        && Paths.Contains(path, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<KnownSettings.Field> Fields(JsonObject root)
    {
        var serilog = KnownSettings.FindNode(root, "Serilog");
        if (serilog is not null and not JsonObject)
        {
            yield break;
        }

        foreach (var name in Paths)
        {
            var path = KnownSettings.ActualPath(root, name);
            var current = KnownSettings.FindNode(root, path);
            if (current is null or JsonArray)
            {
                var value = current ?? KnownSettings.FindNode(Defaults.Value, name) ?? new JsonArray();
                yield return new KnownSettings.Field(path, value.DeepClone());
            }
        }
    }

    public static JsonNode Mask(JsonNode value) => Transform(value, string.Empty, (path, scalar) =>
        path.IsSensitiveConfigurationPath ? JsonValue.Create(Text(scalar).MaskSensitiveValue())! : scalar.DeepClone());

    public static Option<JsonNode> Merge(JsonNode existing, JsonElement incoming)
    {
        if (incoming.ValueKind != JsonValueKind.Array || !HasUniqueProperties(incoming))
        {
            return None;
        }

        var secrets = new Dictionary<string, List<JsonValue>>(StringComparer.OrdinalIgnoreCase);
        CollectSecrets(existing, string.Empty, secrets);
        var ambiguous = false;
        var parsed = JsonNode.Parse(incoming.GetRawText())!;
        var result = Transform(parsed, string.Empty, (path, scalar) =>
        {
            if (!path.IsSensitiveConfigurationPath || !scalar.TryGetValue<string>(out var text)
                || !secrets.TryGetValue(path.Split(':')[^1], out var values))
            {
                return scalar.DeepClone();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                var previous = SamePositionSecret(existing, parsed, path);
                if (previous is null)
                {
                    ambiguous = true;
                    return scalar.DeepClone();
                }
                return previous.DeepClone();
            }

            var matches = values.Where(value => Text(value).MaskSensitiveValue() == text)
                .DistinctBy(value => value.ToJsonString(), StringComparer.Ordinal).ToArray();
            if (matches.Length > 1)
            {
                ambiguous = true;
            }
            return matches.Length == 1 ? matches[0].DeepClone() : scalar.DeepClone();
        });
        return ambiguous ? None : Some(result);
    }

    private static JsonValue? SamePositionSecret(JsonNode existing, JsonNode incoming, string path)
    {
        JsonNode? before = existing;
        JsonNode? after = incoming;
        foreach (var segment in path.Split(':'))
        {
            if (before is JsonObject beforeObject && after is JsonObject afterObject)
            {
                before = beforeObject.FirstOrDefault(pair =>
                    string.Equals(pair.Key, segment, StringComparison.OrdinalIgnoreCase)).Value;
                after = afterObject.FirstOrDefault(pair =>
                    string.Equals(pair.Key, segment, StringComparison.OrdinalIgnoreCase)).Value;
            }
            else if (before is JsonArray beforeArray && after is JsonArray afterArray
                && int.TryParse(segment, out var index) && index >= 0
                && index < beforeArray.Count && index < afterArray.Count
                && SameUniqueIdentity(beforeArray, afterArray, index))
            {
                before = beforeArray[index];
                after = afterArray[index];
            }
            else
            {
                return null;
            }
        }
        return before as JsonValue;
    }

    private static bool SameUniqueIdentity(JsonArray before, JsonArray after, int index)
    {
        var name = EntryName(before[index]);
        return name is not null
            && string.Equals(name, EntryName(after[index]), StringComparison.OrdinalIgnoreCase)
            && before.Count(item => string.Equals(name, EntryName(item), StringComparison.OrdinalIgnoreCase)) == 1
            && after.Count(item => string.Equals(name, EntryName(item), StringComparison.OrdinalIgnoreCase)) == 1;
    }

    private static string? EntryName(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return null;
        }
        var name = obj.FirstOrDefault(pair => string.Equals(pair.Key, "Name", StringComparison.OrdinalIgnoreCase)).Value;
        return name is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    }

    private static void CollectSecrets(JsonNode? node, string path, Dictionary<string, List<JsonValue>> secrets)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    CollectSecrets(value, Join(path, key), secrets);
                }
                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    CollectSecrets(array[index], Join(path, index.ToString()), secrets);
                }
                break;
            case JsonValue value when path.IsSensitiveConfigurationPath:
                var name = path.Split(':')[^1];
                if (!secrets.TryGetValue(name, out var values))
                {
                    values = [];
                    secrets.Add(name, values);
                }
                values.Add(value);
                break;
        }
    }

    private static JsonNode Transform(JsonNode node, string path, Func<string, JsonValue, JsonNode> transform) =>
        node switch
        {
            JsonObject obj => new JsonObject(obj.Select(pair =>
                KeyValuePair.Create(pair.Key, pair.Value is null ? null : Transform(pair.Value, Join(path, pair.Key), transform)))),
            JsonArray array => new JsonArray([..array.Select((value, index) =>
                value is null ? null : Transform(value, Join(path, index.ToString()), transform))]),
            JsonValue value => transform(path, value),
            _ => node.DeepClone()
        };

    private static string Text(JsonValue value) =>
        value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString();

    private static bool HasUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray().All(HasUniqueProperties);
        }
        if (value.ValueKind != JsonValueKind.Object)
        {
            return true;
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return value.EnumerateObject().All(property => names.Add(property.Name) && HasUniqueProperties(property.Value));
    }

    private static string Join(string path, string segment) => path.Length == 0 ? segment : $"{path}:{segment}";
}
