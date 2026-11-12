using System.ComponentModel;
using System.Globalization;
using Instrumentation = Behind.Common.Configuration.OpenTelemetryConfiguration.Instrumentation;

namespace Behind.Common.Converters;

public sealed class OpenTelemetryInstrumentationTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string name)
        {
            return base.ConvertFrom(context, culture, value);
        }

        return name switch
        {
            nameof(Instrumentation.AspNetCoreInstrumentation) => Instrumentation.AspNetCoreInstrumentation,
            nameof(Instrumentation.HttpClientInstrumentation) => Instrumentation.HttpClientInstrumentation,
            nameof(Instrumentation.RuntimeInstrumentation) => Instrumentation.RuntimeInstrumentation,
            nameof(Instrumentation.OtlpExporter) => Instrumentation.OtlpExporter,
            _ => throw new FormatException($"Unknown OpenTelemetry instrumentation '{name}'. Expected AspNetCoreInstrumentation, HttpClientInstrumentation, RuntimeInstrumentation or OtlpExporter.")
        };
    }
}
