using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Configuration;

public static class OptionConfigurationBinder
{
    public static T? GetOptionAware<T>(this IConfiguration configuration)
    {
        object? value = BindValue(typeof(T), configuration);

        return value is null ? default : (T)value;
    }

    private static object? BindValue(Type targetType, IConfiguration configuration)
    {
        if (IsOption(targetType, out Type innerType))
        {
            return BindOption(targetType, innerType, configuration);
        }

        string? scalarValue = GetSectionValue(configuration);

        if (scalarValue is not null)
        {
            return ConvertScalar(targetType, scalarValue);
        }

        if (targetType.IsAbstract && configuration.GetChildren().Any())
        {
            return BindPolymorphic(targetType, configuration);
        }

        if (TryConvertFromChildren(targetType, configuration, out object? converted))
        {
            return converted;
        }

        object? instance = CreateInstance(targetType, configuration);

        if (instance is null)
        {
            return null;
        }

        foreach (PropertyInfo property in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite)
            {
                continue;
            }

            IConfigurationSection childSection = configuration.GetSection(property.Name);

            if (!SectionExists(childSection))
            {
                continue;
            }

            object? propertyValue = BindValue(property.PropertyType, childSection);

            property.SetValue(instance, propertyValue);
        }

        return instance;
    }

    private static object? BindPolymorphic(Type abstractType, IConfiguration configuration)
    {
        string? discriminator = DiscriminatorName(abstractType);
        string? kind = discriminator is null ? null : GetSectionValue(configuration.GetSection(discriminator));

        if (kind is null)
        {
            return null;
        }

        object? concrete = TypeDescriptor
            .GetConverter(abstractType)
            .ConvertFrom(null, CultureInfo.InvariantCulture, kind);

        return concrete is null ? null : BindValue(concrete.GetType(), configuration);
    }

    private static string? DiscriminatorName(Type abstractType)
    {
        return abstractType
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(constructor => constructor.GetParameters())
            .FirstOrDefault(parameters => parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
            ?[0].Name;
    }

    private static bool TryConvertFromChildren(Type targetType, IConfiguration configuration, out object? result)
    {
        TypeConverter converter = TypeDescriptor.GetConverter(targetType);

        if (!converter.CanConvertFrom(typeof(IReadOnlyDictionary<string, string?>)))
        {
            result = null;
            return false;
        }

        Dictionary<string, string?> values = configuration
            .GetChildren()
            .Where(child => child.Value is not null)
            .ToDictionary(child => child.Key, child => child.Value);

        result = converter.ConvertFrom(null, CultureInfo.InvariantCulture, values);
        return true;
    }

    private static object BindOption(
        Type optionType,
        Type innerType,
        IConfiguration configuration)
    {
        if (!SectionExists(configuration))
        {
            return Activator.CreateInstance(optionType)!;
        }

        object? innerValue = BindValue(innerType, configuration);

        if (innerValue is null)
        {
            return Activator.CreateInstance(optionType)!;
        }

        return typeof(FunctionalExtensions)
            .GetMethod(nameof(Some))!
            .MakeGenericMethod(innerType)
            .Invoke(null, [innerValue])!;
    }

    private static object? ConvertScalar(Type targetType, string value)
    {
        Type concreteType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (string.IsNullOrWhiteSpace(value) && targetType != typeof(string))
        {
            return null;
        }

        if (concreteType == typeof(string))
        {
            return value;
        }

        TypeConverter converter = TypeDescriptor.GetConverter(concreteType);

        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFrom(
                null,
                CultureInfo.InvariantCulture,
                value);
        }

        return Convert.ChangeType(
            value,
            concreteType,
            CultureInfo.InvariantCulture);
    }

    private static object? CreateInstance(Type targetType, IConfiguration configuration)
    {
        ConstructorInfo? emptyConstructor = targetType.GetConstructor(Type.EmptyTypes);

        if (emptyConstructor is not null)
        {
            return Activator.CreateInstance(targetType);
        }

        ConstructorInfo? constructor = targetType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .FirstOrDefault();

        if (constructor is null)
        {
            return null;
        }

        ParameterInfo[] parameters = constructor.GetParameters();

        object?[] arguments = parameters
            .Select(parameter =>
            {
                IConfigurationSection childSection = configuration.GetSection(parameter.Name!);

                if (!SectionExists(childSection))
                {
                    return GetDefaultValue(parameter.ParameterType);
                }

                return BindValue(parameter.ParameterType, childSection);
            })
            .ToArray();

        return constructor.Invoke(arguments);
    }

    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static bool IsOption(Type type, out Type innerType)
    {
        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(Option<>))
        {
            innerType = type.GetGenericArguments()[0];
            return true;
        }

        innerType = null!;
        return false;
    }

    private static bool SectionExists(IConfiguration configuration)
    {
        return GetSectionValue(configuration) is not null ||
               configuration.GetChildren().Any();
    }

    private static string? GetSectionValue(IConfiguration configuration)
    {
        return configuration is IConfigurationSection section
            ? section.Value
            : null;
    }
}