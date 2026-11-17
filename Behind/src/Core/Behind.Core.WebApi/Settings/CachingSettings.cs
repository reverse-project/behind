using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common.Configuration;

namespace Behind.Core.WebApi.Settings;

public static class CachingSettings
{
    public const string ProviderPath = "Caching:Provider";
    public const string KeyPath = "Caching:Key";

    public static bool IsCachingFile(string file) =>
        string.Equals(file, ConfigurationFileCatalog.CachingFile, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<SettingsOption>? OptionsFor(string file, string path) =>
        IsCachingFile(file) && string.Equals(path, ProviderPath, StringComparison.OrdinalIgnoreCase)
            ? [new(nameof(Memory), nameof(Memory)), new(nameof(RedisCompatible), nameof(RedisCompatible))]
            : null;

    public static bool IsValid(string file, string path, JsonElement value)
    {
        if (!IsCachingFile(file))
        {
            return true;
        }

        if (string.Equals(path, ProviderPath, StringComparison.OrdinalIgnoreCase))
        {
            return value.ValueKind == JsonValueKind.String
                && (string.Equals(value.GetString(), nameof(Memory), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value.GetString(), nameof(RedisCompatible), StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(path, KeyPath, StringComparison.OrdinalIgnoreCase))
        {
            return value.ValueKind == JsonValueKind.String;
        }

        return path.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase)
            && value.ValueKind == JsonValueKind.String;
    }

    public static JsonValue? DefaultValue(JsonObject root, string file, string path)
    {
        if (!IsCachingFile(file) || root["Caching"] is not null and not JsonObject)
        {
            return null;
        }

        return path switch
        {
            ProviderPath => JsonValue.Create(nameof(Memory)),
            KeyPath => JsonValue.Create(string.Empty),
            _ => null
        };
    }

    public static IEnumerable<SettingsFieldResponse> Missing(JsonObject root, string file)
    {
        foreach (var path in new[] { ProviderPath, KeyPath })
        {
            if (!root.Fields().Any(field => string.Equals(field.Path, path, StringComparison.OrdinalIgnoreCase))
                && DefaultValue(root, file, path) is { } value)
            {
                yield return new SettingsFieldResponse(
                    path, "string", value.GetValue<string>(), false, true, null, OptionsFor(file, path));
            }
        }
    }
}
