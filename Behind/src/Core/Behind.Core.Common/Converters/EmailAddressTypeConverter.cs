using System.ComponentModel;
using System.Globalization;
using Behind.Core.Common.Email;

namespace Behind.Core.Common.Converters;

public sealed class EmailAddressTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string stringValue)
        {
            return new EmailAddress { Address = stringValue };
        }

        return base.ConvertFrom(context, culture, value);
    }
}