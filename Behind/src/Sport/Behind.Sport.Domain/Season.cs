using Behind.Common;

namespace Behind.Sport.Domain;

public sealed record Season(
    VersionedKey<Guid> Id,
    Guid DisciplineId,
    string Name,
    DateOnly? StartsOn,
    DateOnly? EndsOn)
{
    public const string EntityType = nameof(Season);
    public const int MaxNameLength = 100;

    public Season(Guid disciplineId, string name, DateOnly? startsOn, DateOnly? endsOn)
        : this(new VersionedKey<Guid>(Guid.Empty), disciplineId, name, startsOn, endsOn)
    {
    }
}
