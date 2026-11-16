using System.ComponentModel;
using System.Globalization;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Converters;

public sealed class OptionTypeConverter : TypeConverter
{
    private readonly Type _optionType;
    private readonly Type _innerType;

    public OptionTypeConverter(Type optionType)
    {
        if (!optionType.IsGenericType ||
            optionType.GetGenericTypeDefinition() != typeof(Option<>))
        {
            throw new ArgumentException(
                "Option type converter requires an Option<T> target type.",
                nameof(optionType));
        }

        _optionType = optionType;
        _innerType = optionType.GetGenericArguments()[0];
    }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
    {
        if (value is not string optionValue)
        {
            return base.ConvertFrom(context, culture, value);
        }

        if (string.IsNullOrWhiteSpace(optionValue))
        {
            return CreateNoneOption();
        }

        object? innerValue;

        try
        {
            innerValue = ConvertInnerValue(optionValue, culture);
        }
        catch (Exception exception) when (
            exception is FormatException or
            NotSupportedException or
            InvalidCastException)
        {
            throw new FormatException(
                $"Value '{optionValue}' cannot be converted to Option<{_innerType.Name}>.",
                exception);
        }

        return innerValue is null
            ? CreateNoneOption()
            : CreateSomeOption(innerValue);
    }

    private object CreateNoneOption()
    {
        return Activator.CreateInstance(_optionType)!;
    }

    private object CreateSomeOption(object value)
    {
        return typeof(FunctionalExtensions)
            .GetMethod(nameof(Some))!
            .MakeGenericMethod(_innerType)
            .Invoke(null, [value])!;
    }

    private object? ConvertInnerValue(
        string value,
        CultureInfo? culture)
    {
        Type targetType = Nullable.GetUnderlyingType(_innerType) ?? _innerType;

        if (targetType == typeof(string))
        {
            return value;
        }

        TypeConverter converter = TypeDescriptor.GetConverter(targetType);

        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFrom(
                null,
                culture ?? CultureInfo.InvariantCulture,
                value);
        }

        throw new NotSupportedException(
            $"Type '{targetType.Name}' cannot be converted from a string.");
    }
}