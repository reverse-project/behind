using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using Behind.Common.Configuration;
using Behind.Common.WebApi.Infrastructure.StaticFiles;
using Behind.Core.Common.Configuration;

namespace Behind.Core.WebApi.Settings;

public static class KnownSettings
{
    private static readonly JsonObject Schema = ConfigurationSchema.Describe();

    private static readonly IReadOnlyDictionary<string, string[]> Sections =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["backend.json"] = ["Backend"],
            ["appsettings.json"] = ["AllowedHosts"],
            ["frontend.json"] = ["Frontend"],
            ["security.json"] = ["Security"],
            ["database.json"] = ["Database", "ConnectionStrings"],
            ["caching.json"] = ["Caching", "ConnectionStrings"],
            ["forum.json"] = ["Forum"],
            ["markdownviewer.json"] = ["MarkdownViewer"]
        };

    private static readonly IReadOnlyDictionary<string, object> Defaults = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
    {
        ["Backend"] = new BackendConfiguration(),
        ["Backend:Accounts"] = new AccountsConfiguration(),
        ["Backend:Mcp"] = new McpConfiguration(),
        ["Backend:Comments"] = new CommentsConfiguration(),
        ["Backend:ContentRestrictions"] = new ContentRestrictionsConfiguration(),
        ["Backend:Downloads"] = new DownloadsConfiguration(),
        ["Backend:Scheduler"] = new SchedulerConfiguration(),
        ["Frontend:Sites"] = new SitesConfiguration(),
        ["Frontend:Galleries"] = new GalleriesConfiguration(),
        ["Frontend:Carousels"] = new CarouselsConfiguration(),
        ["Frontend:StaticFiles"] = FrontendOptions.Default,
        ["Security"] = new SecurityConfiguration(),
        ["Security:Accounts"] = new SecurityAccountSettings(),
        ["Security:MagicLink:RotationInterval"] = MagicLinkSettings.DefaultRotationInterval.ToString(),
        ["Security:MagicLink:RotationOverlap"] = MagicLinkSettings.DefaultRotationOverlap.ToString(),
        ["Security:MagicLink:LinkLifetime"] = MagicLinkSettings.DefaultLinkLifetime.ToString(),
        ["Security:MagicLink:RequestCooldown"] = MagicLinkSettings.DefaultRequestCooldown.ToString(),
        ["Security:MagicLink:RequestLimit"] = MagicLinkSettings.DefaultRequestLimit,
        ["Security:MagicLink:RequestWindow"] = MagicLinkSettings.DefaultRequestWindow.ToString(),
        ["Security:MagicLink:RedeemLimit"] = MagicLinkSettings.DefaultRedeemLimit,
        ["Security:MagicLink:RedeemWindow"] = MagicLinkSettings.DefaultRedeemWindow.ToString(),
        ["Caching:Provider"] = nameof(Memory),
        ["AllowedHosts"] = "*"
    };

    public sealed record Field(string Path, JsonNode Value);

    public static bool IsManagedAppSettingsPath(string path) =>
        path.Equals("Serilog:MinimumLevel", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("Serilog:MinimumLevel:", StringComparison.OrdinalIgnoreCase)
        || path.Equals("OpenTelemetry", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("OpenTelemetry:", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<Field> Fields(JsonObject root, string file, IReadOnlyList<string> moduleKeys)
    {
        var result = new List<Field>();
        var sections = Sections.GetValueOrDefault(file);
        if (sections is null)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            sections = [..Schema["properties"]!.AsObject()
                .Where(property => string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase))
                .Select(property => property.Key)];
        }
        foreach (var section in sections)
        {
            if (Schema["properties"]?[section] is JsonObject definition)
            {
                AddFields(result, root, section, definition, string.Empty);
            }
        }

        if (string.Equals(file, ConfigurationFileCatalog.AppSettingsFile, StringComparison.OrdinalIgnoreCase))
        {
            result.AddRange(SerilogSettings.Fields(root));
        }

        if (string.Equals(file, ConfigurationFileCatalog.BackendFile, StringComparison.OrdinalIgnoreCase)
            && CanTraverse(root, ["Backend", "Modules"]))
        {
            foreach (var key in moduleKeys.Where(key => !string.IsNullOrWhiteSpace(key) && !key.Contains(':')))
            {
                var path = ActualPath(root, $"Backend:Modules:{key}");
                var value = FindNode(root, path);
                if (value is null || value is JsonValue scalar && scalar.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
                {
                    result.Add(new Field(path, value?.DeepClone() ?? JsonValue.Create(true)!));
                }
            }
        }

        if (string.Equals(file, ConfigurationFileCatalog.BackendFile, StringComparison.OrdinalIgnoreCase))
        {
            AddMcpFields(result, root);
        }

        return result;
    }

    private static void AddMcpFields(List<Field> fields, JsonObject root)
    {
        var defaults = new Dictionary<string, object>
        {
            ["Backend:Accounts:ServiceUser"] = new AccountsConfiguration().ServiceUser,
            ["Backend:Mcp:Enabled"] = new McpConfiguration().Enabled,
            ["Backend:Mcp:MaxRequestBytes"] = new McpConfiguration().MaxRequestBytes,
            ["Backend:Mcp:MaxResponseBytes"] = new McpConfiguration().MaxResponseBytes,
            ["Backend:Mcp:AllowedOrigins"] = new McpConfiguration().AllowedOrigins
        };
        foreach (var (path, fallback) in defaults)
        {
            if (fields.Any(field => field.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                || !CanTraverse(root, path.Split(':')[..^1]))
            {
                continue;
            }
            var actual = ActualPath(root, path);
            var current = FindNode(root, actual);
            var expected = JsonSerializer.SerializeToNode(fallback)!;
            if (current is null || Matches(current, expected is JsonArray ? "array"
                : fallback is bool ? "boolean" : "integer"))
            {
                fields.Add(new Field(actual, current?.DeepClone() ?? expected));
            }
        }
    }

    public static JsonNode? FindNode(JsonObject root, string path)
    {
        JsonNode? current = root;
        foreach (var segment in path.Split(':'))
        {
            if (current is JsonArray array && int.TryParse(segment, out var index) && index >= 0 && index < array.Count)
            {
                current = array[index];
                continue;
            }
            if (current is not JsonObject obj)
            {
                return null;
            }

            var actual = obj.FirstOrDefault(pair => string.Equals(pair.Key, segment, StringComparison.OrdinalIgnoreCase));
            current = actual.Value;
        }

        return current;
    }

    public static string ActualPath(JsonObject root, string path)
    {
        JsonNode? current = root;
        var result = new List<string>();
        foreach (var segment in path.Split(':'))
        {
            if (current is JsonArray array && int.TryParse(segment, out var index) && index >= 0 && index < array.Count)
            {
                result.Add(segment);
                current = array[index];
                continue;
            }
            var actual = current is JsonObject obj
                ? obj.FirstOrDefault(pair => string.Equals(pair.Key, segment, StringComparison.OrdinalIgnoreCase))
                : default;
            result.Add(actual.Key ?? segment);
            current = actual.Value;
        }
        return string.Join(':', result);
    }

    private static void AddFields(List<Field> fields, JsonObject parent, string name, JsonObject schema, string prefix)
    {
        var actual = parent.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase));
        var path = prefix.Length == 0 ? actual.Key ?? name : $"{prefix}:{actual.Key ?? name}";
        if (path.Equals("Backend:Modules", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var current = actual.Value;
        var resolved = Resolve(schema);
        var properties = resolved["properties"] as JsonObject;
        if (properties is not null)
        {
            if (current is not null and not JsonObject)
            {
                return;
            }

            var obj = current as JsonObject ?? new JsonObject();
            foreach (var property in properties)
            {
                AddFields(fields, obj, property.Key, property.Value!.AsObject(), path);
            }
            return;
        }

        var kind = Kind(resolved);
        if (kind is null || current is not null && !Matches(current, kind))
        {
            return;
        }

        var value = current?.DeepClone() ?? Default(path, schema, kind);
        fields.Add(new Field(path, value));
    }

    private static JsonObject Resolve(JsonObject schema)
    {
        while (schema["$ref"] is JsonValue reference)
        {
            schema = Schema["$defs"]![reference.GetValue<string>().Split('/')[^1]]!.AsObject();
        }
        return schema;
    }

    private static string? Kind(JsonObject schema)
    {
        var types = schema["type"] is JsonArray array
            ? array.Select(item => item!.GetValue<string>())
            : schema["type"] is JsonValue value ? [value.GetValue<string>()] : [];
        return types.FirstOrDefault(type => type != "null");
    }

    private static bool Matches(JsonNode value, string kind) =>
        kind switch
        {
            "object" => value is JsonObject,
            "array" => value is JsonArray,
            "string" => value is JsonValue scalar && scalar.GetValueKind() == JsonValueKind.String,
            "boolean" => value is JsonValue scalar && scalar.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
            "integer" or "number" => value is JsonValue scalar && scalar.GetValueKind() == JsonValueKind.Number,
            _ => false
        };

    private static JsonNode Default(string path, JsonObject schema, string kind)
    {
        if (Defaults.TryGetValue(path, out var direct))
        {
            return JsonSerializer.SerializeToNode(direct)!;
        }

        var split = path.LastIndexOf(':');
        if (split > 0 && Defaults.TryGetValue(path[..split], out var owner))
        {
            var property = owner.GetType().GetProperty(path[(split + 1)..],
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var value = property?.GetValue(owner);
            if (value is not null)
            {
                var serialized = JsonSerializer.SerializeToNode(value);
                if (serialized is not null && Matches(serialized, kind))
                {
                    return serialized;
                }
            }
        }

        if (schema["default"] is { } declared)
        {
            return declared.DeepClone();
        }

        return kind switch
        {
            "boolean" => JsonValue.Create(false)!,
            "number" or "integer" => JsonValue.Create(0)!,
            "array" => new JsonArray(),
            "object" => new JsonObject(),
            _ => JsonValue.Create(string.Empty)!
        };
    }

    private static bool CanTraverse(JsonObject root, string[] path)
    {
        JsonNode? current = root;
        foreach (var part in path)
        {
            if (current is not JsonObject obj)
            {
                return false;
            }
            current = obj.FirstOrDefault(pair => string.Equals(pair.Key, part, StringComparison.OrdinalIgnoreCase)).Value;
            if (current is null)
            {
                return true;
            }
        }
        return current is JsonObject;
    }
}
