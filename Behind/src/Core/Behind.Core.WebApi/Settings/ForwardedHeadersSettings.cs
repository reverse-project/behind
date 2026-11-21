using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Behind.Core.WebApi.Settings;

public sealed record ForwardedHeadersSettings(
    [property: JsonRequired] bool Enabled,
    [property: JsonRequired] string[] KnownForwarders,
    [property: JsonRequired] int ForwardLimit)
{
    public static bool IsManagedPath(string path) =>
        string.Equals(path, ForwardedHeadersConfiguration.SectionName, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith($"{ForwardedHeadersConfiguration.SectionName}:", StringComparison.OrdinalIgnoreCase);

    public static async Task<ForwardedHeadersConfiguration> ReadAsync(FileInfo file, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllBytesAsync(file.FullName, cancellationToken);
        using var stream = new MemoryStream(content, writable: false);
        using var configuration = new ConfigurationManager();
        configuration.AddJsonStream(stream);
        return configuration.GetSection(ForwardedHeadersConfiguration.SectionName)
            .Get<ForwardedHeadersConfiguration>() ?? new();
    }

    public static void Write(JsonObject root, ForwardedHeadersConfiguration configuration)
    {
        foreach (var key in root.Select(pair => pair.Key).Where(IsManagedPath).ToArray())
        {
            root.Remove(key);
        }

        var backendKey = FindPropertyName(root, "Backend");
        if (root[backendKey] is not JsonObject backend)
        {
            backend = new JsonObject();
            root[backendKey] = backend;
        }

        foreach (var key in backend.Select(pair => pair.Key)
                     .Where(key => key.StartsWith("ForwardedHeaders:", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            backend.Remove(key);
        }

        backend[FindPropertyName(backend, "ForwardedHeaders")] = JsonSerializer.SerializeToNode(configuration);
    }

    private static string FindPropertyName(JsonObject parent, string name) =>
        parent.Select(pair => pair.Key).FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
        ?? name;
}
