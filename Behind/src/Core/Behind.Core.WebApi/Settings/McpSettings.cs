using System.Text.Json;
using Behind.Core.Common.Configuration;

namespace Behind.Core.WebApi.Settings;

public static class McpSettings
{
    public static bool IsValid(string file, string path, JsonElement value)
    {
        if (!string.Equals(file, ConfigurationFileCatalog.BackendFile, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.Equals($"{McpConfiguration.SectionName}:MaxRequestBytes", StringComparison.OrdinalIgnoreCase)
            || path.Equals($"{McpConfiguration.SectionName}:MaxResponseBytes", StringComparison.OrdinalIgnoreCase))
        {
            return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0;
        }

        if (path.Equals($"{McpConfiguration.SectionName}:AllowedOrigins", StringComparison.OrdinalIgnoreCase))
        {
            return value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(item =>
                item.ValueKind == JsonValueKind.String && IsOrigin(item.GetString()!));
        }

        return true;
    }

    private static bool IsOrigin(string value)
    {
        if (value.Any(char.IsWhiteSpace) || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var authorityStart = value.IndexOf("://", StringComparison.Ordinal);
        return authorityStart > 0 && value[(authorityStart + 3)..].IndexOfAny(['/', '?', '#', '\\']) < 0;
    }
}
