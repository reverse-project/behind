using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Behind.Core.Database.Converters;

public sealed class ScheduleParticipantsConverter() : ValueConverter<ScheduleParticipants, string>(
    participants => participants.ToJson(),
    json => ScheduleExtensions.ParseScheduleParticipants(json));
