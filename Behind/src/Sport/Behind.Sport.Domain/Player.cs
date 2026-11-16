using Behind.Common;

namespace Behind.Sport.Domain;

public sealed record Player(
    VersionedKey<Guid> Id,
    Guid DisciplineId,
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    string? Nationality,
    Guid? PositionId,
    int? HeightCm,
    int? WeightKg,
    Side? DominantSide,
    string? Biography)
{
    public const string EntityType = nameof(Player);
    public const int MaxNameLength = 100;
    public const int MaxNationalityLength = 100;
    public const int MaxBiographyLength = 4_000;
    public const int MinHeightCm = 50;
    public const int MaxHeightCm = 300;
    public const int MinWeightKg = 20;
    public const int MaxWeightKg = 400;

    public string FullName => $"{FirstName} {LastName}";

    public Player(
        Guid disciplineId,
        string firstName,
        string lastName,
        DateOnly? birthDate,
        string? nationality,
        Guid? positionId,
        int? heightCm,
        int? weightKg,
        Side? dominantSide,
        string? biography)
        : this(
            new VersionedKey<Guid>(Guid.Empty),
            disciplineId,
            firstName,
            lastName,
            birthDate,
            nationality,
            positionId,
            heightCm,
            weightKg,
            dominantSide,
            biography)
    {
    }
}
