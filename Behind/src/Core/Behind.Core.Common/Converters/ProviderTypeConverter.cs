using System.ComponentModel;
using System.Globalization;
using Behind.Core.Common.Configuration;

namespace Behind.Core.Common.Converters;

public sealed class ProviderTypeConverter : TypeConverter
{
    private static readonly string[] PostgresNames = [nameof(Postgres), "Npgsql"];

    public override bool CanConvertFrom(
        ITypeDescriptorContext? context,
        Type sourceType)
    {
        return sourceType == typeof(string) ||
               base.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(
        ITypeDescriptorContext? context,
        Type? destinationType)
    {
        return destinationType == typeof(string) ||
               base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        if (value is not string provider)
        {
            return base.ConvertFrom(context, culture, value);
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new FormatException("Provider cannot be empty.");
        }

        if (provider.Contains(nameof(Sqlite), StringComparison.OrdinalIgnoreCase))
        {
            return new Sqlite();
        }

        if (PostgresNames.Any(name => provider.Contains(name, StringComparison.OrdinalIgnoreCase)))
        {
            return new Postgres();
        }

        throw new FormatException(
            $"Provider value '{provider}' is not supported. Expected a value containing '{nameof(Sqlite)}' or '{nameof(Postgres)}'.");
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType)
    {
        if (destinationType == typeof(string) && value is Provider provider)
        {
            return provider.Value;
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
