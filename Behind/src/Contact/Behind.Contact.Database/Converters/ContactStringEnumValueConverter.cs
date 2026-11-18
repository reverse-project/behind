using System.ComponentModel;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Behind.Contact.Database.Converters;

public sealed class ContactStringEnumValueConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => TypeDescriptor.GetConverter(typeof(TEnum)).ConvertToInvariantString(value)!,
    token => (TEnum)TypeDescriptor.GetConverter(typeof(TEnum)).ConvertFromInvariantString(token)!)
    where TEnum : struct, Enum;
