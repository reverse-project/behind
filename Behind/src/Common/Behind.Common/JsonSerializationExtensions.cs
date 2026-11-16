namespace Behind.Common;

using System.Text.Json;
using System.Text.Json.Serialization;
using Behind.Common.Converters;

public static class JsonSerializationExtensions
{
    public static JsonSerializerOptions WithSensitiveContext(
        this JsonSerializerOptions options,
        SensitiveContext? context)
    {
        if (context is null || !context.IsActive)
        {
            return options;
        }

        if (!options.Converters.OfType<SensibleJsonConverterFactory>().Any())
        {
            options.Converters.Add(new SensibleJsonConverterFactory());
        }

        return options;
    }
}
