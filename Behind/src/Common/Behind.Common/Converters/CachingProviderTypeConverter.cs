using System.ComponentModel;
using System.Globalization;
using Behind.Common.Configuration;

namespace Behind.Common.Converters;

public sealed class CachingProviderTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string provider)
        {
            return base.ConvertFrom(context, culture, value);
        }

        if (string.Equals(provider, nameof(Memory), StringComparison.OrdinalIgnoreCase))
        {
            return new Memory();
        }

        if (string.Equals(provider, nameof(RedisCompatible), StringComparison.OrdinalIgnoreCase))
        {
            return new RedisCompatible();
        }

        throw new FormatException("Caching provider must be Memory or RedisCompatible.");
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is CachingProvider provider)
        {
            return provider.Value;
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
