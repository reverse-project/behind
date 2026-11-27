using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common;
using Behind.Common.Configuration;
using Behind.Common.WebApi.Infrastructure.Localization;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Settings;

public static class SettingsResponseExtensions
{
    extension(FileInfo file)
    {
        public async Task<SettingsFileResponse> ToSettingsFile(
            IReadOnlyList<LocaleInfo> locales,
            IReadOnlyList<string>? moduleKeys = null,
            CancellationToken cancellationToken = default)
        {
            var root = await file.ReadJsonAsync(cancellationToken);
            var known = KnownSettings.Fields(root, file.Name, moduleKeys ?? []);
            var paths = known.Select(field => field.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var arrays = known.Where(field => field.Value is JsonArray).Select(field => field.Path + ":").ToArray();

            return new SettingsFileResponse(
                file.Name,
                [
                    ..root.Fields().Where(field => (!string.Equals(file.Name, ConfigurationFileCatalog.AppSettingsFile, StringComparison.OrdinalIgnoreCase)
                            || !KnownSettings.IsManagedAppSettingsPath(field.Path))
                        && !paths.Contains(field.Path)
                        && !arrays.Any(prefix => field.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        && !field.Path.Equals("Backend:Modules", StringComparison.OrdinalIgnoreCase)
                        && !field.Path.StartsWith("Backend:Modules:", StringComparison.OrdinalIgnoreCase)
                        && !OidcSettingsExtensions.IsOidcPath(file.Name, field.Path)
                        && !ForwardedHeadersSettings.IsManagedPath(field.Path))
                        .Select(field => field.ToResponse(file.Name, locales)),
                    ..known.Where(field => !OidcSettingsExtensions.IsOidcPath(file.Name, field.Path)
                        && !ForwardedHeadersSettings.IsManagedPath(field.Path))
                        .Select(field => field.ToResponse(file.Name, locales))
                ]);
        }
    }

    extension(JsonConfigurationField field)
    {
        private SettingsFieldResponse ToResponse(string file, IReadOnlyList<LocaleInfo> locales)
        {
            var action = file.FindAction(field.Path);
            var sensitive = field.Path.IsSensitiveConfigurationPath
                && !AccountModeSettings.IsPasswordSwitch(file, field.Path, field.Value);

            return new SettingsFieldResponse(
                field.Path,
                field.Value.TypeName,
                sensitive ? field.Value.Text.MaskSensitiveValue() : field.Value.Text,
                sensitive,
                action == None,
                action.Match<string?>(() => null, found => found.Name),
                CachingSettings.OptionsFor(file, field.Path) ?? locales.OptionsFor(file, field.Path));
        }
    }

    extension(KnownSettings.Field field)
    {
        private SettingsFieldResponse ToResponse(string file, IReadOnlyList<LocaleInfo> locales)
        {
            if (field.Value is JsonValue scalar)
            {
                return new JsonConfigurationField(field.Path, scalar).ToResponse(file, locales);
            }

            var sensitive = field.Path.IsSensitiveConfigurationPath;
            var shown = SerilogSettings.IsCollection(file, field.Path)
                ? SerilogSettings.Mask(field.Value)
                : field.Value.DeepClone();
            if (sensitive && shown is JsonObject map)
            {
                foreach (var (key, value) in map.ToArray())
                {
                    if (value is JsonValue item && item.TryGetValue<string>(out var text))
                    {
                        map[key] = text.MaskSensitiveValue();
                    }
                }
            }

            return new SettingsFieldResponse(field.Path, field.Value is JsonArray ? "array" : "object",
                shown.ToJsonString(), sensitive, true, null);
        }
    }

    extension(JsonValue value)
    {
        private string TypeName => value.GetValueKind().ToTypeName();

        private string Text =>
            value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString();
    }

    extension(JsonValueKind kind)
    {
        private string ToTypeName() =>
            kind switch
            {
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Number => "number",
                _ => "string"
            };
    }
}
