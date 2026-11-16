using System.Net.Mail;
using System.Text.Json;
using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public static class ScheduleExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<string, ScheduleNotification> Notifications =
        Enum.GetValues<ScheduleNotification>().ToDictionary(value => value.Key, value => value, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, ScheduleParticipantKind> ParticipantKinds =
        Enum.GetValues<ScheduleParticipantKind>().ToDictionary(value => value.Key, value => value, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, ScheduleRunStatus> RunStatuses =
        Enum.GetValues<ScheduleRunStatus>().ToDictionary(value => value.Key, value => value, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, ScheduleRunEmail> RunEmails =
        Enum.GetValues<ScheduleRunEmail>().ToDictionary(value => value.Key, value => value, StringComparer.OrdinalIgnoreCase);

    extension(ScheduleNotification notification)
    {
        public string Key => JsonNamingPolicy.CamelCase.ConvertName(notification.ToString());

        public bool Wants(bool succeeded) =>
            notification switch
            {
                ScheduleNotification.Always => true,
                ScheduleNotification.Success => succeeded,
                ScheduleNotification.Failure => !succeeded,
                _ => false
            };
    }

    extension(ScheduleParticipantKind kind)
    {
        public string Key => JsonNamingPolicy.CamelCase.ConvertName(kind.ToString());
    }

    extension(ScheduleRunStatus status)
    {
        public string Key => JsonNamingPolicy.CamelCase.ConvertName(status.ToString());
    }

    extension(ScheduleRunEmail email)
    {
        public string Key => JsonNamingPolicy.CamelCase.ConvertName(email.ToString());
    }

    extension(string? key)
    {
        public Option<ScheduleNotification> ToScheduleNotification() => Lookup(Notifications, key);

        public Option<ScheduleParticipantKind> ToScheduleParticipantKind() => Lookup(ParticipantKinds, key);

        public Option<ScheduleRunStatus> ToScheduleRunStatus() => Lookup(RunStatuses, key);

        public Option<ScheduleRunEmail> ToScheduleRunEmail() => Lookup(RunEmails, key);

        public bool IsScheduleEmailAddress =>
            !string.IsNullOrWhiteSpace(key)
            && key.Length <= ScheduleParticipant.MaxValueLength
            && MailAddress.TryCreate(key.Trim(), out var address)
            && string.Equals(address.Address, key.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    extension(ScheduleParticipant participant)
    {
        public bool IsValid =>
            participant.Kind switch
            {
                ScheduleParticipantKind.Role or ScheduleParticipantKind.User =>
                    Guid.TryParse(participant.Value, out var id) && id != Guid.Empty,
                ScheduleParticipantKind.Email => participant.Value.IsScheduleEmailAddress,
                _ => false
            };

        public Option<Guid> Identifier =>
            participant.Kind is not ScheduleParticipantKind.Email && Guid.TryParse(participant.Value, out var id)
                ? Some(id)
                : None;
    }

    extension(ScheduleParticipants participants)
    {
        public bool IsValid => participants.Items.All(participant => participant.IsValid);

        public bool IsEmpty => participants.Items.Count == 0;

        public IReadOnlyList<Guid> RoleIds => participants.IdsOf(ScheduleParticipantKind.Role);

        public IReadOnlyList<Guid> UserIds => participants.IdsOf(ScheduleParticipantKind.User);

        public IReadOnlyList<string> Emails =>
            participants.Items
                .Where(participant => participant.Kind == ScheduleParticipantKind.Email)
                .Select(participant => participant.Value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        public ScheduleParticipants Normalized =>
            new(participants.Items
                .Select(participant => participant with
                {
                    Value = participant.Kind == ScheduleParticipantKind.Email
                        ? participant.Value.Trim()
                        : Guid.TryParse(participant.Value, out var id) ? id.ToString() : participant.Value.Trim()
                })
                .DistinctBy(participant => (participant.Kind, participant.Value.ToUpperInvariant()))
                .ToList());

        public string ToJson() =>
            JsonSerializer.Serialize(
                participants.Items.Select(participant => new ParticipantJson(participant.Kind.Key, participant.Value)),
                SerializerOptions);

        private IReadOnlyList<Guid> IdsOf(ScheduleParticipantKind kind) =>
            participants.Items
                .Where(participant => participant.Kind == kind)
                .Bind(participant => participant.Identifier)
                .Distinct()
                .ToList();
    }

    extension(ScheduleParameters parameters)
    {
        public string ToJson() =>
            JsonSerializer.Serialize(
                new SortedDictionary<string, string>(parameters.Values.ToDictionary(), StringComparer.Ordinal),
                SerializerOptions);
    }

    extension(Schedule schedule)
    {
        public Option<DateTimeOffset> NextRunAfter(DateTimeOffset moment) =>
            schedule.Enabled ? schedule.Cron.Next(moment) : None;
    }

    public static ScheduleParticipants ParseScheduleParticipants(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? ScheduleParticipants.Empty
            : new ScheduleParticipants(
                (JsonSerializer.Deserialize<List<ParticipantJson>>(json, SerializerOptions) ?? [])
                .Select(item => item.Kind.ToScheduleParticipantKind().Match(
                    () => throw new FormatException($"Unknown participant kind '{item.Kind}'."),
                    kind => new ScheduleParticipant(kind, item.Value ?? string.Empty)))
                .ToList());

    public static ScheduleParameters ParseScheduleParameters(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? ScheduleParameters.Empty
            : new ScheduleParameters(
                JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions)
                ?? new Dictionary<string, string>());

    private static Option<T> Lookup<T>(IReadOnlyDictionary<string, T> values, string? key) =>
        key is not null && values.TryGetValue(key.Trim(), out var value) ? Some(value) : None;

    private sealed record ParticipantJson(string Kind, string? Value);
}
