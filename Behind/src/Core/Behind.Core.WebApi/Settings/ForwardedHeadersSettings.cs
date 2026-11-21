using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Behind.Core.Common.Configuration;

namespace Behind.Core.WebApi.Settings;

public sealed record ForwardedHeadersSettings(
    [property: JsonRequired] bool Enabled,
    [property: JsonRequired] string[] KnownForwarders,
    [property: JsonRequired] int ForwardLimit)
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    public static bool IsManagedPath(string path) =>
        string.Equals(path, ForwardedHeadersConfiguration.SectionName, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith($"{ForwardedHeadersConfiguration.SectionName}:", StringComparison.OrdinalIgnoreCase);

    public static ForwardedHeadersConfiguration Read(JsonObject root) =>
        Section(root).Value?.Deserialize<ForwardedHeadersConfiguration>(ReadOptions) ?? new();

    public static (string Path, JsonNode? Value) Section(JsonObject root)
    {
        var backend = Property(root, "Backend");
        var section = Property(backend.Value as JsonObject, "ForwardedHeaders");
        return ($"{backend.Key}:{section.Key}", section.Value);
    }

    private static KeyValuePair<string, JsonNode?> Property(JsonObject? parent, string name) =>
        parent?.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)) is { Key: not null } pair
            ? pair : KeyValuePair.Create<string, JsonNode?>(name, null);
}
