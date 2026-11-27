using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common;
using Behind.Common.Configuration;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Settings;

public static class SettingsChangeExtensions
{
    extension(JsonObject root)
    {
        public Option<IReadOnlyDictionary<string, JsonNode?>> PlanChanges(
            string file,
            IReadOnlyDictionary<string, JsonElement> values,
            IReadOnlyList<string>? moduleKeys = null)
        {
            var known = KnownSettings.Fields(root, file, moduleKeys ?? []);
            var planned = values.Select(pair => root.PlanChange(file, pair.Key, pair.Value, known, moduleKeys ?? [])).ToList();
            var overlapping = values.Keys.Any(path => values.Keys.Any(other =>
                other.StartsWith(path + ":", StringComparison.OrdinalIgnoreCase)));

            return overlapping || values.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count
                || planned.Any(change => change == None)
                ? None
                : Some<IReadOnlyDictionary<string, JsonNode?>>(
                    planned.SelectMany(change => change.AsEnumerable()).ToDictionary());
        }

        private Option<KeyValuePair<string, JsonNode?>> PlanChange(
            string file, string path, JsonElement incoming, IReadOnlyList<KnownSettings.Field> known,
            IReadOnlyList<string> moduleKeys)
        {
            var modulePath = path.Equals("Backend:Modules", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Backend:Modules:", StringComparison.OrdinalIgnoreCase);
            var allowedModule = !modulePath || string.Equals(file, ConfigurationFileCatalog.BackendFile, StringComparison.OrdinalIgnoreCase)
                && moduleKeys.Any(key => string.Equals(path, $"Backend:Modules:{key}", StringComparison.OrdinalIgnoreCase));
            var isEditable = file.FindAction(path) == None && !OidcSettingsExtensions.IsOidcPath(file, path)
                && !ForwardedHeadersSettings.IsManagedPath(path)
                && (!string.Equals(file, ConfigurationFileCatalog.AppSettingsFile, StringComparison.OrdinalIgnoreCase)
                    || !KnownSettings.IsManagedAppSettingsPath(path))
                && allowedModule && CachingSettings.IsValid(file, path, incoming);
            var keepsSecret = path.IsSensitiveConfigurationPath && incoming.IsBlank;
            var field = known.FirstOrDefault(field => string.Equals(field.Path, path, StringComparison.OrdinalIgnoreCase));
            var existing = KnownSettings.FindNode(root, path) ?? field?.Value;
            if (isEditable && existing is JsonArray && field is not null && SerilogSettings.IsCollection(file, path))
            {
                return SerilogSettings.Merge(existing, incoming).Map(node => KeyValuePair.Create(field.Path, (JsonNode?)node));
            }
            if (existing is JsonObject or JsonArray && field is null)
            {
                return None;
            }
            if (existing is JsonObject && incoming.ValueKind == JsonValueKind.Object && path.IsSensitiveConfigurationPath)
            {
                return isEditable
                    ? MergeSensitiveMap(existing.AsObject(), incoming).Map(node =>
                        KeyValuePair.Create(field?.Path ?? KnownSettings.ActualPath(root, path), (JsonNode?)node))
                    : None;
            }

            return isEditable && !keepsSecret && existing is not null
                ? existing.Coerce(incoming).Map(node =>
                    KeyValuePair.Create(field?.Path ?? KnownSettings.ActualPath(root, path), (JsonNode?)node))
                : None;
        }
    }

    private static Option<JsonNode> MergeSensitiveMap(JsonObject existing, JsonElement incoming)
    {
        var result = new JsonObject();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in incoming.EnumerateObject())
        {
            if (property.Name.Length == 0 || property.Name.Contains(':')
                || property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)
                || !names.Add(property.Name))
            {
                return None;
            }

            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                result[property.Name] = null;
                continue;
            }

            var text = property.Value.GetString()!;
            var old = existing.FirstOrDefault(pair =>
                string.Equals(pair.Key, property.Name, StringComparison.OrdinalIgnoreCase)).Value;
            if (old is JsonValue scalar && scalar.TryGetValue<string>(out var previous)
                && (string.IsNullOrWhiteSpace(text) || text == previous.MaskSensitiveValue()))
            {
                var mask = previous.MaskSensitiveValue();
                var ambiguous = !string.IsNullOrWhiteSpace(text)
                    && existing
                        .Select(pair => pair.Value)
                        .OfType<JsonValue>()
                        .Select(value => value.TryGetValue<string>(out var candidate) ? candidate : null)
                        .Where(candidate => candidate is not null)
                        .Distinct(StringComparer.Ordinal)
                        .Count(candidate => candidate!.MaskSensitiveValue() == mask) > 1;
                if (ambiguous)
                {
                    return None;
                }
                text = previous;
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                var matches = existing
                    .Select(pair => pair.Value)
                    .OfType<JsonValue>()
                    .Select(value => value.TryGetValue<string>(out var candidate) ? candidate : null)
                    .Where(candidate => candidate is not null && text == candidate.MaskSensitiveValue())
                    .ToList();
                if (matches.Count > 1)
                {
                    return None;
                }
                if (matches.Count == 1)
                {
                    text = matches[0]!;
                }
                else if (matches.Count == 0 && IsSensitiveMask(text))
                {
                    return None;
                }
            }
            result[property.Name] = text;
        }
        return Some<JsonNode>(result);
    }

    private static bool IsSensitiveMask(string text)
    {
        if (text == "....")
        {
            return true;
        }
        if (text.Length is >= 3 and <= 5)
        {
            return text[0] != '.'
                && text[^1] != '.'
                && text[1..^1].All(character => character == '.');
        }
        return text.Length >= 6
            && text[2..^2].All(character => character == '.')
            && text.Length == 2 + text[2..^2].Length + 2;
    }

    extension(JsonElement element)
    {
        private bool IsBlank =>
            element.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(element.GetString());
    }

    extension(JsonNode existing)
    {
        private Option<JsonNode> Coerce(JsonElement incoming) =>
            (existing.GetValueKind(), incoming.ValueKind) switch
            {
                (JsonValueKind.String, JsonValueKind.String) => Some<JsonNode>(JsonValue.Create(incoming.GetString()!)!),
                (JsonValueKind.Number, JsonValueKind.Number) => Some(JsonNode.Parse(incoming.GetRawText())!),
                (JsonValueKind.True or JsonValueKind.False, JsonValueKind.True or JsonValueKind.False) =>
                    JsonValue.Create(incoming.GetBoolean()),
                (JsonValueKind.Array, JsonValueKind.Array) or (JsonValueKind.Object, JsonValueKind.Object) =>
                    Some<JsonNode>(JsonNode.Parse(incoming.GetRawText())!),
                _ => None
            };
    }
}
