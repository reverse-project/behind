using System.ComponentModel;
using System.Globalization;
using Behind.Core.Domain;

namespace Behind.Core.Database.Converters;

public sealed class AuditLogEntryAuditActionTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value) =>
        value is string text
            ? AuditLogEntryAuditActionJsonConverter.FromStorageValue(text)
            : base.ConvertFrom(context, culture, value);

    public override object? ConvertTo(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object? value,
        Type destinationType) =>
        destinationType == typeof(string) && value is AuditLogEntry.AuditAction action
            ? AuditLogEntryAuditActionJsonConverter.ToStorageValue(action)
            : base.ConvertTo(context, culture, value, destinationType);
}