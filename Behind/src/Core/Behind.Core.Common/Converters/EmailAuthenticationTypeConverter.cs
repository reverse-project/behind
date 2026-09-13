using System.ComponentModel;
using System.Globalization;
using Behind.Core.Common.Configuration;
namespace Behind.Core.Common.Converters;

public sealed class EmailAuthenticationTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string stringValue)
        {
            return stringValue switch
            {
                "No" => new NoAuthentication(),
                "Basic" => context?.Instance is BasicAuthentication basic 
                    ? new BasicAuthentication(basic.Username, basic.Password)
                    : new BasicAuthentication("", ""),
                _ => throw new ArgumentException("Invalid authentication type", nameof(value))
            };
        }

        return base.ConvertFrom(context, culture, value);
    }
}