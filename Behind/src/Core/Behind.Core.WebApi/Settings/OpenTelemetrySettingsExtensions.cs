using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common;
using Behind.Common.Configuration;
using OpenTelemetry.Exporter;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Settings;

public static class OpenTelemetrySettingsExtensions
{
    private static readonly IReadOnlyList<string> ProtocolNames = Enum.GetNames<OtlpExportProtocol>();

    private static readonly IReadOnlyList<string> InstrumentNames =
    [
        "AspNetCoreInstrumentation",
        "HttpClientInstrumentation",
        "RuntimeInstrumentation",
        "OtlpExporter"
    ];

    private static readonly OpenTelemetrySettingsRequest EmptyConfiguration =
        new(
            string.Empty,
            false,
            new OpenTelemetryExporterSettingsRequest(false, null, null, []),
            new OpenTelemetryExporterSettingsRequest(false, null, null, []),
            new OpenTelemetryLoggingSettingsRequest(false, null, null, [], null, null, null));

    extension(FileInfo file)
    {
        public async Task<OpenTelemetrySettingsResponse> ReadOpenTelemetrySettingsAsync(
            CancellationToken cancellationToken = default)
        {
            var root = await file.ReadJsonAsync(cancellationToken);
            var configuration = root[OpenTelemetryConfiguration.SectionName]?
                                   .Deserialize<OpenTelemetrySettingsRequest>()
                               ?? EmptyConfiguration;

            return configuration.ToSettings();
        }

        public Task WriteOpenTelemetrySettingsAsync(
            OpenTelemetrySettingsRequest request,
            CancellationToken cancellationToken = default) =>
            file.SetJsonNodesAsync(
                new Dictionary<string, JsonNode?>
                {
                    [OpenTelemetryConfiguration.SectionName] =
                        JsonSerializer.SerializeToNode(request)
                },
                cancellationToken);
    }

    extension(OpenTelemetrySettingsRequest request)
    {
        public Option<OpenTelemetrySettingsRequest> ToNormalizedRequest()
        {
            var normalized = request with
            {
                ServiceName = request.ServiceName?.Trim(),
                Traces = request.Traces?.Normalize(),
                Metrics = request.Metrics?.Normalize(),
                Logging = request.Logging?.Normalize()
            };

            return normalized.IsValid ? Some(normalized) : None;
        }

        private OpenTelemetrySettingsResponse ToSettings() =>
            new(
                request.ServiceName ?? string.Empty,
                request.IsEnabled,
                request.Traces.ToSettings(),
                request.Metrics.ToSettings(),
                request.Logging.ToSettings(),
                ProtocolNames,
                InstrumentNames);

        private bool IsValid =>
            (!request.IsEnabled || !string.IsNullOrWhiteSpace(request.ServiceName))
            && request.Traces is not null
            && request.Metrics is not null
            && request.Logging is not null
            && request.Traces.IsValid
            && request.Metrics.IsValid
            && request.Logging.IsValid;
    }

    extension(OpenTelemetryExporterSettingsRequest? exporter)
    {
        private OpenTelemetryExporterSettings ToSettings() =>
            new(
                exporter?.IsEnabled ?? false,
                exporter?.Endpoint,
                exporter?.Protocol,
                exporter?.Instruments ?? []);

        private OpenTelemetryExporterSettingsRequest? Normalize() =>
            exporter is null
                ? null
                : exporter with
                {
                    Endpoint = NormalizeEndpoint(exporter.Endpoint),
                    Protocol = NormalizeProtocol(exporter.Protocol),
                    Instruments = NormalizeInstruments(exporter.Instruments)
                };

        private bool IsValid =>
            exporter is not null
            && exporter.Protocol.IsValidProtocol
            && exporter.Endpoint.IsValidEndpoint(exporter.IsEnabled && exporter.Instruments?.Contains("OtlpExporter") == true)
            && exporter.Instruments.IsValidInstruments;
    }

    extension(OpenTelemetryLoggingSettingsRequest? logging)
    {
        private OpenTelemetryLoggingSettings ToSettings() =>
            new(
                logging?.IsEnabled ?? false,
                logging?.Endpoint,
                logging?.Protocol,
                logging?.Instruments ?? [],
                logging?.IncludeFormattedMessage,
                logging?.IncludeScopes,
                logging?.ParseStateValues);

        private OpenTelemetryLoggingSettingsRequest? Normalize() =>
            logging is null
                ? null
                : logging with
                {
                    Endpoint = NormalizeEndpoint(logging.Endpoint),
                    Protocol = NormalizeProtocol(logging.Protocol),
                    Instruments = NormalizeInstruments(logging.Instruments)
                };

        private bool IsValid =>
            logging is not null
            && logging.Protocol.IsValidProtocol
            && logging.Endpoint.IsValidEndpoint(logging.IsEnabled && logging.Instruments?.Contains("OtlpExporter") == true)
            && logging.Instruments.IsValidInstruments;
    }

    extension(string? protocol)
    {
        private bool IsValidProtocol =>
            string.IsNullOrWhiteSpace(protocol) || ProtocolNames.Contains(protocol, StringComparer.Ordinal);

        private static string? NormalizeProtocol(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    extension(string? endpoint)
    {
        private bool IsValidEndpoint(bool required) =>
            string.IsNullOrWhiteSpace(endpoint)
                ? !required
                : Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
                  && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        private static string? NormalizeEndpoint(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    extension(IReadOnlyList<string>? instruments)
    {
        private bool IsValidInstruments =>
            instruments is not null
            && instruments.All(InstrumentNames.Contains)
            && instruments.Distinct(StringComparer.Ordinal).Count() == instruments.Count;

        private static IReadOnlyList<string> NormalizeInstruments(IReadOnlyList<string>? values) =>
            values?.Select(value => value.Trim()).ToArray() ?? [];
    }
}
