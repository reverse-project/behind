using System.ComponentModel;

namespace Behind.Common.Converters;

public sealed class OptionTypeDescriptionProvider : TypeDescriptionProvider
{
    private static readonly TypeDescriptionProvider DefaultProvider =
        TypeDescriptor.GetProvider(typeof(object));

    public override ICustomTypeDescriptor GetTypeDescriptor(
        Type objectType,
        object? instance)
    {
        ICustomTypeDescriptor defaultDescriptor =
            DefaultProvider.GetTypeDescriptor(objectType, instance)!;

        return new OptionTypeDescriptor(objectType, defaultDescriptor);
    }

    private sealed class OptionTypeDescriptor(
        Type optionType,
        ICustomTypeDescriptor parent) : CustomTypeDescriptor(parent)
    {
        public override TypeConverter GetConverter()
        {
            return new OptionTypeConverter(optionType);
        }
    }
}
