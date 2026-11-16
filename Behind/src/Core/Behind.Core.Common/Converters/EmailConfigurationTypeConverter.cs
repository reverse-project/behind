using System.ComponentModel;
using Behind.Core.Common.Configuration;

namespace Behind.Core.Common.Converters;

public sealed class EmailConfigurationTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
    {
        if (value is string stringValue)
        {
            return stringValue switch
            {
                "Smtp" => context?.Instance is SmtpEmailConfiguration smtpConfig
                    ? smtpConfig
                    : new SmtpEmailConfiguration("", 0, new Ssl(false), new NoAuthentication(), new()),
                "Logger" => new LoggerEmailConfiguration(),
                _ => throw new NotSupportedException($"Unsupported email configuration type: {stringValue}")
            };
        }
        return base.ConvertFrom(context, culture, value);
    }
}