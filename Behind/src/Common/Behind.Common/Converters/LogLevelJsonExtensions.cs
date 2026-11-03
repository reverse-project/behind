using System.Text.Json;
using Behind.Common.Logging;
using Serilog.Events;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Converters;

internal static class LogLevelJsonExtensions
{
    extension(JsonElement element)
    {
        public Option<LogEventLevel> ToLevel() =>
            element.ValueKind == JsonValueKind.String && MinimumLevelConfiguration.TryParseLevel(element.GetString(), out var level)
                ? Some(level)
                : None;
    }
}
