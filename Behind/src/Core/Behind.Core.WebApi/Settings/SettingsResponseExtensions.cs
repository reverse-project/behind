using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common;
using Behind.Common.Configuration;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Settings;

public static class SettingsResponseExtensions
{
    extension(FileInfo file)
    {
        public async Task<SettingsFileResponse> ToSettingsFile(CancellationToken cancellationToken = default)
        {
            var root = await file.ReadJsonAsync(cancellationToken);

            return new SettingsFileResponse(
                file.Name,
                [
                    ..root.Fields().Select(field => field.ToResponse(file.Name)),
                    ..file.Name.MissingActions(root).Select(action => action.ToMissingField())
                ]);
        }
    }

    extension(string file)
    {
        private IEnumerable<SettingsAction> MissingActions(JsonObject root) =>
            SettingsAction.All.Where(action =>
                action.CreatesMissing
                && file.FindAction(action.Path) != None
                && root.FindValue(action.Path) is null);
    }

    extension(JsonConfigurationField field)
    {
        private SettingsFieldResponse ToResponse(string file)
        {
            var action = file.FindAction(field.Path);
            var sensitive = field.Path.IsSensitiveConfigurationPath;

            return new SettingsFieldResponse(
                field.Path,
                field.Value.TypeName,
                sensitive ? field.Value.Text.MaskSensitiveValue() : field.Value.Text,
                sensitive,
                action == None,
                action.Match<string?>(() => null, found => found.Name));
        }
    }

    extension(SettingsAction action)
    {
        private SettingsFieldResponse ToMissingField() =>
            new(action.Path, JsonValueKind.String.ToTypeName(), string.Empty, true, false, action.Name);
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
