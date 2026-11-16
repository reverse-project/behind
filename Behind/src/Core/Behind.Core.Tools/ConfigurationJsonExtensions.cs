using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Behind.Common.Converters;

namespace Behind.Core.Tools;

public static class ConfigurationJsonExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new OptionJsonConverterFactory() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SettableMembersOnly } }
    };

    extension<T>(T configuration)
    {
        public string ToConfigurationJson() =>
            JsonSerializer.Serialize(configuration, JsonOptions);
    }

    // Computed properties of the configuration records (limits, effective values) are not configuration keys.
    private static void SettableMembersOnly(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (var property in typeInfo.Properties.Where(property => property.Set is null).ToList())
        {
            typeInfo.Properties.Remove(property);
        }
    }
}
