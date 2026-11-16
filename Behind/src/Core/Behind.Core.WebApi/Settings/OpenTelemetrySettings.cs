using System.ComponentModel.DataAnnotations;

namespace Behind.Core.WebApi.Settings;

public sealed record OpenTelemetryExporterSettings(
    bool IsEnabled,
    string? Endpoint,
    string? Protocol,
    IReadOnlyList<string> Instruments);

public sealed record OpenTelemetryLoggingSettings(
    bool IsEnabled,
    string? Endpoint,
    string? Protocol,
    IReadOnlyList<string> Instruments,
    bool? IncludeFormattedMessage,
    bool? IncludeScopes,
    bool? ParseStateValues);

public sealed record OpenTelemetrySettingsResponse(
    string ServiceName,
    bool IsEnabled,
    OpenTelemetryExporterSettings Traces,
    OpenTelemetryExporterSettings Metrics,
    OpenTelemetryLoggingSettings Logging,
    IReadOnlyList<string> Protocols,
    IReadOnlyList<string> Instruments);

public sealed record OpenTelemetryExporterSettingsRequest(
    bool IsEnabled,
    string? Endpoint,
    string? Protocol,
    IReadOnlyList<string>? Instruments);

public sealed record OpenTelemetryLoggingSettingsRequest(
    bool IsEnabled,
    string? Endpoint,
    string? Protocol,
    IReadOnlyList<string>? Instruments,
    bool? IncludeFormattedMessage,
    bool? IncludeScopes,
    bool? ParseStateValues);

public sealed record OpenTelemetrySettingsRequest(
    [property: StringLength(256)] string? ServiceName,
    bool IsEnabled,
    [property: Required] OpenTelemetryExporterSettingsRequest? Traces,
    [property: Required] OpenTelemetryExporterSettingsRequest? Metrics,
    [property: Required] OpenTelemetryLoggingSettingsRequest? Logging);
