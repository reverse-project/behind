using Behind.Common;

namespace Behind.Sport.Domain;

public sealed record Position(
    VersionedKey<Guid> Id,
    Guid DisciplineId,
    string Name,
    string Abbreviation,
    int SortOrder)
{
    public const string EntityType = nameof(Position);
    public const int MaxNameLength = 100;
    public const int MaxAbbreviationLength = 10;

    public Position(Guid disciplineId, string name, string abbreviation, int sortOrder)
        : this(new VersionedKey<Guid>(Guid.Empty), disciplineId, name, abbreviation, sortOrder)
    {
    }
}
