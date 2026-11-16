using System.ComponentModel;
using System.Globalization;

namespace Behind.Core.Domain.Converters;

public sealed class ScheduleParametersTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string json
            ? ScheduleExtensions.ParseScheduleParameters(json)
            : base.ConvertFrom(context, culture, value);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is ScheduleParameters parameters
            ? parameters.ToJson()
            : base.ConvertTo(context, culture, value, destinationType);
}
