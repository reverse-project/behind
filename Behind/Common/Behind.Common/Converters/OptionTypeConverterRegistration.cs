using System.ComponentModel;

namespace Behind.Common.Converters;

public static class OptionTypeConverterRegistration
{
    public static void Register<T>()
    {
        TypeDescriptor.AddProvider(
            new OptionTypeDescriptionProvider(),
            typeof(Option<T>));
    }
}
