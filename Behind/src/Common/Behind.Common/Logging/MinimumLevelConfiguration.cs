using System.Text.Json.Serialization;
using Behind.Common.Converters;
using Serilog.Events;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Logging;

[JsonConverter(typeof(MinimumLevelJsonConverter))]
public sealed record MinimumLevelConfiguration(
    Option<LogEventLevel> Default,
    IReadOnlyDictionary<string, LogEventLevel> Overrides)
{
    public const string SectionPath = "Serilog:MinimumLevel";

    public static readonly IReadOnlyList<LogEventLevel> Levels = Enum.GetValues<LogEventLevel>();

    public static readonly MinimumLevelConfiguration Empty = new(default, new Dictionary<string, LogEventLevel>());

    public bool IsEmpty => Default == None && Overrides.Count == 0;

    public static bool TryParseLevel(string? value, out LogEventLevel level) =>
        Enum.TryParse(value, true, out level) && Enum.IsDefined(level) && !int.TryParse(value, out _);
}
