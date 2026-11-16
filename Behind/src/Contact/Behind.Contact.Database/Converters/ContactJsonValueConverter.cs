using System.ComponentModel;
using Behind.Contact.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Behind.Contact.Database.Converters;

public sealed class ContactJsonValueConverter<TValue, TItem>() : ValueConverter<TValue, string>(
    value => TypeDescriptor.GetConverter(typeof(TValue)).ConvertToInvariantString(value)!,
    json => Deserialize(json))
    where TValue : class, IContactJsonArray<TValue, TItem>
{
    private static TValue Deserialize(string json) =>
        (TValue)(TypeDescriptor.GetConverter(typeof(TValue)).ConvertFromInvariantString(json)
            ?? throw new InvalidOperationException($"The persisted {typeof(TValue).Name} value must not be null."));
}
