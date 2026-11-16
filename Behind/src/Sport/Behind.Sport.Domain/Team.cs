using Behind.Common;

namespace Behind.Sport.Domain;

public sealed record Team(
    VersionedKey<Guid> Id,
    Guid DisciplineId,
    string Name,
    string? ShortName,
    string? City,
    string? Country,
    int? FoundedYear,
    string? Description,
    Guid? LogoId)
{
    public const string EntityType = nameof(Team);
    public const int MaxNameLength = 150;
    public const int MaxShortNameLength = 20;
    public const int MaxCityLength = 100;
    public const int MaxCountryLength = 100;
    public const int MaxDescriptionLength = 2_000;
    public const int MinFoundedYear = 1800;
    public const int MaxFoundedYear = 2200;

    public Team(
        Guid disciplineId,
        string name,
        string? shortName,
        string? city,
        string? country,
        int? foundedYear,
        string? description,
        Guid? logoId)
        : this(
            new VersionedKey<Guid>(Guid.Empty),
            disciplineId,
            name,
            shortName,
            city,
            country,
            foundedYear,
            description,
            logoId)
    {
    }
}
