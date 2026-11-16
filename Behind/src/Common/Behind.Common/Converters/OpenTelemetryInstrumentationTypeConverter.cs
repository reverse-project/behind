using System.ComponentModel;
using System.Globalization;
using Instrumentation = Behind.Common.Configuration.OpenTelemetryConfiguration.Instrumentation;

namespace Behind.Common.Converters;

public sealed class OpenTelemetryInstrumentationTypeConverter : TypeConverter
{
    public static readonly OpenTelemetryInstrumentationTypeConverter Instance = new();

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

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

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
    {
        if (destinationType != typeof(string) || value is not Instrumentation instrumentation)
        {
            return base.ConvertTo(context, culture, value, destinationType);
        }

        return instrumentation switch
        {
            var item when item == Instrumentation.AspNetCoreInstrumentation =>
                nameof(Instrumentation.AspNetCoreInstrumentation),
            var item when item == Instrumentation.HttpClientInstrumentation =>
                nameof(Instrumentation.HttpClientInstrumentation),
            var item when item == Instrumentation.RuntimeInstrumentation =>
                nameof(Instrumentation.RuntimeInstrumentation),
            var item when item == Instrumentation.OtlpExporter =>
                nameof(Instrumentation.OtlpExporter),
            _ => throw new FormatException($"Unknown OpenTelemetry instrumentation '{instrumentation}'.")
        };
    }
}
