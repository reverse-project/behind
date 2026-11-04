using System.Text.Json;
using Behind.Common.Converters;

namespace Behind.Core.Tools;

public static class ConfigurationJsonExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new OptionJsonConverterFactory() }
    };

    extension<T>(T configuration)
    {
        public string ToConfigurationJson() =>
            JsonSerializer.Serialize(configuration, JsonOptions);
    }
}
