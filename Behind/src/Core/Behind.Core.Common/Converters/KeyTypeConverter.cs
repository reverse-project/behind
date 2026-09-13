using System.ComponentModel;
using System.Globalization;
using Behind.Core.Common.Configuration;

namespace Behind.Core.Common.Converters;

public sealed class KeyTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        return value is string key
            ? new Key(key)
            : base.ConvertFrom(context, culture, value);
    }
}