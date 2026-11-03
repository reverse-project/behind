using System.ComponentModel;
using System.Globalization;

namespace Behind.Common.Templating;

public sealed class TemplatesTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(IReadOnlyDictionary<string, string?>) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not IReadOnlyDictionary<string, string?> values)
        {
            return base.ConvertFrom(context, culture, value);
        }

        var templates = new Templates();

        foreach (var (name, content) in values.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            templates.Add(name, content!);
        }

        return templates;
    }
}
