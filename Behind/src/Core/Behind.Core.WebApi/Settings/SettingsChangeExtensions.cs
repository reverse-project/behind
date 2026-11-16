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
            IReadOnlyDictionary<string, JsonElement> values)
        {
            var planned = values.Select(pair => root.PlanChange(file, pair.Key, pair.Value)).ToList();

            return planned.Any(change => change == None)
                ? None
                : Some<IReadOnlyDictionary<string, JsonNode?>>(
                    planned.SelectMany(change => change.AsEnumerable()).ToDictionary());
        }

        private Option<KeyValuePair<string, JsonNode?>> PlanChange(string file, string path, JsonElement incoming)
        {
            var isEditable = file.FindAction(path) == None;
            var keepsSecret = path.IsSensitiveConfigurationPath && incoming.IsBlank;
            var existing = root.FindValue(path);
            if (existing is null && AccountModeSettings.CanAdd(root, file, path))
            {
                existing = JsonValue.Create(true);
            }

            return isEditable && !keepsSecret && existing is not null
                ? existing.Coerce(incoming).Map(node => KeyValuePair.Create(path, (JsonNode?)node))
                : None;
        }
    }

    extension(JsonElement element)
    {
        private bool IsBlank =>
            element.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(element.GetString());
    }

    extension(JsonValue existing)
    {
        private Option<JsonNode> Coerce(JsonElement incoming) =>
            (existing.GetValueKind(), incoming.ValueKind) switch
            {
                (JsonValueKind.String, JsonValueKind.String) => Some<JsonNode>(JsonValue.Create(incoming.GetString()!)!),
                (JsonValueKind.Number, JsonValueKind.Number) => Some(JsonNode.Parse(incoming.GetRawText())!),
                (JsonValueKind.True or JsonValueKind.False, JsonValueKind.True or JsonValueKind.False) =>
                    JsonValue.Create(incoming.GetBoolean()),
                _ => None
            };
    }
}
