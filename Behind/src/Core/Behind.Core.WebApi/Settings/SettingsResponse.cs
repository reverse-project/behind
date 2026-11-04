using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Behind.Core.WebApi.Settings;

public sealed record SettingsFieldResponse(
    string Path,
    string Type,
    string Value,
    bool Sensitive,
    bool Editable,
    string? Action,
    IReadOnlyList<SettingsOption>? Options = null);

public sealed record SettingsOption(string Value, string Label);

public sealed record SettingsFileResponse(string Name, IReadOnlyList<SettingsFieldResponse> Fields, string? Editor = null);

public sealed record SettingsResponse(bool ReadOnly, IReadOnlyList<SettingsFileResponse> Files);

public sealed record SettingsUpdateRequest([property: Required] IReadOnlyDictionary<string, JsonElement> Values);

public sealed record SettingsActionRequest([property: Required, MinLength(1)] string Path);

public sealed record SettingsActionResponse(string Path, string? OtpUri);
