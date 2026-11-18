using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface IScheduleParticipantQueries
{
    public sealed record ParticipantRole(Guid Id, string Name);

    public sealed record ParticipantUser(Guid Id, string Label);

    public sealed record ParticipantDirectory(IReadOnlyList<ParticipantRole> Roles, IReadOnlyList<ParticipantUser> Users);

    public sealed record LabeledParticipant(ScheduleParticipantKind Kind, string Value, string Label);

    Task<ParticipantDirectory> Directory(string? search, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LabeledParticipant>> Label(
        ScheduleParticipants participants,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> Emails(
        ScheduleParticipants participants,
        CancellationToken cancellationToken = default);
}
