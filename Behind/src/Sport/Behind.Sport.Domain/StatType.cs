using Behind.Common;

namespace Behind.Sport.Domain;

public sealed record StatType(
    VersionedKey<Guid> Id,
    Guid DisciplineId,
    string Name,
    string Abbreviation,
    StatScope Scope,
    bool HigherIsBetter)
{
    public const string EntityType = nameof(StatType);
    public const int MaxNameLength = 100;
    public const int MaxAbbreviationLength = 10;

    public StatType(Guid disciplineId, string name, string abbreviation, StatScope scope, bool higherIsBetter)
        : this(new VersionedKey<Guid>(Guid.Empty), disciplineId, name, abbreviation, scope, higherIsBetter)
    {
    }
}
