using System.ComponentModel;
using System.Text.Json.Serialization;
using Behind.Common;
using Behind.Common.Scheduling;
using Behind.Core.Domain.Converters;

namespace Behind.Core.Domain;

public enum ScheduleNotification
{
    Never,
    Always,
    Success,
    Failure
}

public enum ScheduleParticipantKind
{
    Role,
    User,
    Email
}

public sealed record ScheduleParticipant(ScheduleParticipantKind Kind, string Value)
{
    public const int MaxValueLength = 320;
}

[TypeConverter(typeof(ScheduleParticipantsTypeConverter))]
[JsonConverter(typeof(ScheduleParticipantsJsonConverter))]
public sealed record ScheduleParticipants(IReadOnlyList<ScheduleParticipant> Items)
{
    public static readonly ScheduleParticipants Empty = new([]);

    public bool Equals(ScheduleParticipants? other) =>
        other is not null && Items.SequenceEqual(other.Items);

    public override int GetHashCode() =>
        Items.Aggregate(0, (hash, item) => HashCode.Combine(hash, item));
}

[TypeConverter(typeof(ScheduleParametersTypeConverter))]
[JsonConverter(typeof(ScheduleParametersJsonConverter))]
public sealed record ScheduleParameters(IReadOnlyDictionary<string, string> Values)
{
    public static readonly ScheduleParameters Empty = new(new Dictionary<string, string>());

    public bool Equals(ScheduleParameters? other) =>
        other is not null
        && Values.Count == other.Values.Count
        && Values.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SequenceEqual(other.Values.OrderBy(entry => entry.Key, StringComparer.Ordinal));

    public override int GetHashCode() =>
        Values.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Aggregate(0, (hash, entry) => HashCode.Combine(hash, entry.Key, entry.Value));
}

public sealed record Schedule(
    VersionedKey<Guid> Id,
    string Name,
    CronExpression Cron,
    string Action,
    ScheduleParameters Parameters,
    bool Enabled,
    ScheduleNotification Notify,
    ScheduleParticipants Participants)
{
    public const string EntityType = nameof(Schedule);
    public const int MaxNameLength = 200;
    public const int MaxCronLength = 200;
    public const int MaxActionLength = 100;
    public const int MaxParametersLength = 20_000;
    public const int MaxParticipantsLength = 50_000;
    public const int MaxNotifyLength = 16;

    public Schedule(
        string name,
        CronExpression cron,
        string action,
        ScheduleParameters parameters,
        bool enabled,
        ScheduleNotification notify,
        ScheduleParticipants participants)
        : this(new VersionedKey<Guid>(Guid.Empty), name, cron, action, parameters, enabled, notify, participants)
    {
    }
}
