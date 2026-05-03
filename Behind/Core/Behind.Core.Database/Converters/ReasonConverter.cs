using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Behind.Core.Database.Converters;

public sealed class ReasonConverter() : ValueConverter<Reason, string>(
    reason => reason.Value,
    value => new Reason(value)
);