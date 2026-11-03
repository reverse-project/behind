using System.ComponentModel.DataAnnotations;

namespace Behind.Core.WebApi.Settings;

public sealed record LoggingOverrideSettings(string Source, string Level);

public sealed record LoggingSettingsResponse(
    string? Default,
    IReadOnlyList<string> Levels,
    IReadOnlyList<LoggingOverrideSettings> Overrides);

public sealed record LoggingSettingsRequest(
    string? Default,
    [property: Required] IReadOnlyList<LoggingOverrideSettings> Overrides);
