using System.Text.Json;
using System.Text.Json.Nodes;
using Behind.Common;
using Behind.Common.Configuration;
using Behind.Common.Logging;
using Serilog.Events;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Settings;

public static class LoggingSettingsExtensions
{
    private static readonly JsonSerializerOptions WriteOptions = new();

    extension(FileInfo file)
    {
        public async Task<MinimumLevelConfiguration> ReadMinimumLevelAsync(CancellationToken cancellationToken = default)
        {
            var root = await file.ReadJsonAsync(cancellationToken);

            return root.FindNode(MinimumLevelConfiguration.SectionPath)?.Deserialize<MinimumLevelConfiguration>()
                   ?? MinimumLevelConfiguration.Empty;
        }

        public Task WriteMinimumLevelAsync(MinimumLevelConfiguration configuration, CancellationToken cancellationToken = default) =>
            file.SetJsonNodesAsync(
                new Dictionary<string, JsonNode?>
                {
                    [MinimumLevelConfiguration.SectionPath] = configuration.IsEmpty
                        ? null
                        : JsonSerializer.SerializeToNode(configuration, WriteOptions)
                },
                cancellationToken);

        public Task WriteAppSettingsAsync(
            MinimumLevelConfiguration logging,
            OpenTelemetrySettingsRequest openTelemetry,
            CancellationToken cancellationToken = default) =>
            file.SetJsonNodesAsync(
                new Dictionary<string, JsonNode?>
                {
                    [MinimumLevelConfiguration.SectionPath] = logging.IsEmpty
                        ? null
                        : JsonSerializer.SerializeToNode(logging, WriteOptions),
                    [OpenTelemetryConfiguration.SectionName] = JsonSerializer.SerializeToNode(openTelemetry, WriteOptions)
                },
                cancellationToken);
    }

    extension(MinimumLevelConfiguration configuration)
    {
        public LoggingSettingsResponse ToSettings() =>
            new(
                configuration.Default.Match<string?>(() => null, level => level.ToString()),
                [..MinimumLevelConfiguration.Levels.Select(level => level.ToString())],
                [
                    ..configuration.Overrides
                        .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(pair => new LoggingOverrideSettings(pair.Key, pair.Value.ToString()))
                ]);
    }

    extension(LoggingSettingsRequest request)
    {
        public Option<MinimumLevelConfiguration> ToConfiguration()
        {
            var overrides = request.Overrides
                .Select(item => (Source: item.Source?.Trim() ?? string.Empty, Level: item.Level.ToLevel()))
                .ToList();

            var isValid = IsValidDefault(request.Default)
                          && overrides.All(item => item.Source != string.Empty && item.Level != None)
                          && overrides.Select(item => item.Source).Distinct(StringComparer.OrdinalIgnoreCase).Count() == overrides.Count;

            return isValid
                ? Some(new MinimumLevelConfiguration(
                    request.Default.ToLevel(),
                    overrides.ToDictionary(
                        item => item.Source,
                        item => item.Level.Match(() => default, level => level))))
                : None;
        }
    }

    extension(JsonObject root)
    {
        private JsonNode? FindNode(string path) =>
            path.Split(':').Aggregate((JsonNode?)root, (current, segment) => (current as JsonObject)?[segment]);
    }

    extension(string? value)
    {
        private Option<LogEventLevel> ToLevel() =>
            MinimumLevelConfiguration.TryParseLevel(value, out var level) ? Some(level) : None;
    }

    private static bool IsValidDefault(string? value) =>
        string.IsNullOrWhiteSpace(value) || MinimumLevelConfiguration.TryParseLevel(value, out _);
}
