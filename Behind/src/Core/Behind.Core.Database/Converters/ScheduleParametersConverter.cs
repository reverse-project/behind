using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Behind.Core.Database.Converters;

public sealed class ScheduleParametersConverter() : ValueConverter<ScheduleParameters, string>(
    parameters => parameters.ToJson(),
    json => ScheduleExtensions.ParseScheduleParameters(json));
