using System.ComponentModel;
using System.Globalization;
using Behind.Core.Common.Configuration;

namespace Behind.Core.Common.Converters;

public sealed class SecretKeyTypeConverter : TypeConverter
{
    private const string Separator = "://";

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        if (value is not string secretKey)
        {
            return base.ConvertFrom(context, culture, value);
        }

        string[] parts = secretKey.Split(Separator, 2);

        if (parts.Length != 2)
        {
            throw new FormatException(
                $"Secret key must be formatted as '<Type>{Separator}<Value>'.");
        }

        string type = parts[0];
        string secretValue = parts[1];

        return type switch
        {
            _ when string.Equals(type, nameof(Totp), StringComparison.OrdinalIgnoreCase)
                   && Totp.TryCreate(secretValue, out var totp) => totp,

            _ when string.Equals(type, nameof(Totp), StringComparison.OrdinalIgnoreCase) =>
                throw new FormatException("The configured TOTP secret key is not a valid Base32 value."),

            _ when string.Equals(type, nameof(Uuid), StringComparison.OrdinalIgnoreCase) &&
                   Uuid.TryCreate(secretValue, out var uuid) => uuid,
            
            _ when string.Equals(type, nameof(Uuid), StringComparison.OrdinalIgnoreCase) =>
                throw new FormatException("The configured UUID secret key is not a valid UUID value."),

            _ => throw new FormatException($"Unsupported secret key type '{type}'.")
        };
    }
}