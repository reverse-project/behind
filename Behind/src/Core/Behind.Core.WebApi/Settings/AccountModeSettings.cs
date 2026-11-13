using System.Text.Json.Nodes;
using System.Text.Json;
using Behind.Common.Configuration;
using Behind.Core.Common.Configuration;

namespace Behind.Core.WebApi.Settings;

public static class AccountModeSettings
{
    public static readonly IReadOnlyList<string> Paths =
    [
        $"{AccountsConfiguration.SectionName}:{nameof(AccountsConfiguration.Password)}",
        $"{AccountsConfiguration.SectionName}:{nameof(AccountsConfiguration.MagicLink)}",
        $"{AccountsConfiguration.SectionName}:{nameof(AccountsConfiguration.TwoFactor)}",
        $"{AccountsConfiguration.SectionName}:{nameof(AccountsConfiguration.Passkey)}",
        $"{AccountsConfiguration.SectionName}:{nameof(AccountsConfiguration.EmailTwoFactor)}"
    ];

    public static bool CanAdd(JsonObject root, string file, string path)
    {
        if (!string.Equals(file, "backend.json", StringComparison.OrdinalIgnoreCase) || !Paths.Contains(path))
        {
            return false;
        }

        if (root["Backend"] is not null and not JsonObject)
        {
            return false;
        }

        var backend = root["Backend"] as JsonObject;
        if (backend?["Accounts"] is not null and not JsonObject)
        {
            return false;
        }

        var accounts = backend?["Accounts"] as JsonObject;
        var name = path[(path.LastIndexOf(':') + 1)..];
        return accounts?[name] is null;
    }

    public static bool IsPasswordSwitch(string file, string path, JsonValue value) =>
        string.Equals(file, "backend.json", StringComparison.OrdinalIgnoreCase)
        && path == $"{AccountsConfiguration.SectionName}:{nameof(AccountsConfiguration.Password)}"
        && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False;

    public static IEnumerable<SettingsFieldResponse> Missing(JsonObject root, string file) =>
        Paths.Where(path => root.FindValue(path) is null && CanAdd(root, file, path))
            .Select(path => new SettingsFieldResponse(path, "boolean", "true", false, true, null));
}
