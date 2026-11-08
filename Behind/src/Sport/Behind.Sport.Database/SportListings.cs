using Behind.Sport.Domain;

namespace Behind.Sport.Database;

public sealed record DisciplineListing(Guid Id, string Name, string Slug, bool IsDeleted);

public sealed record PositionListing(
    Guid Id,
    Guid DisciplineId,
    string DisciplineName,
    string Name,
    string Abbreviation,
    int SortOrder,
    bool IsDeleted);

public sealed record StatTypeListing(
    Guid Id,
    Guid DisciplineId,
    string DisciplineName,
    string Name,
    string Abbreviation,
    StatScope Scope,
    bool HigherIsBetter,
    bool IsDeleted);

public sealed record SeasonListing(
    Guid Id,
    Guid DisciplineId,
    string DisciplineName,
    string Name,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    bool IsDeleted);

public sealed record TeamListing(
    Guid Id,
    Guid DisciplineId,
    string DisciplineName,
    string Name,
    string? ShortName,
    string? City,
    string? Country,
    int? FoundedYear,
    string? Description,
    Guid? LogoId,
    bool IsDeleted)
{
    public string? LogoUrl => LogoId is { } logo ? SportFileTypes.TeamImage(logo) : null;
}

public sealed record PlayerListing(
    Guid Id,
    Guid DisciplineId,
    string DisciplineName,
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    int? Age,
    string? Nationality,
    Guid? PositionId,
    string? PositionName,
    int? HeightCm,
    int? WeightKg,
    Side? DominantSide,
    string? Biography,
    Guid? TeamId,
    string? TeamName,
    string? PictureUrl,
    bool IsDeleted)
{
    public string Name => $"{FirstName} {LastName}";
}

public sealed record PlayerSeasonListing(
    Guid Id,
    Guid PlayerId,
    string PlayerName,
    Guid? PositionId,
    string? PositionName,
    Guid SeasonId,
    string SeasonName,
    Guid TeamId,
    string TeamName,
    int? ShirtNumber,
    Guid? PictureId,
    bool IsDeleted)
{
    public string? PictureUrl => PictureId is { } picture ? SportFileTypes.PlayerImage(picture) : null;
}

public sealed record StatListing(
    Guid Id,
    Guid SeasonId,
    string SeasonName,
    Guid StatTypeId,
    string StatTypeName,
    string StatTypeAbbreviation,
    StatScope Scope,
    Guid SubjectId,
    string SubjectName,
    double Value,
    bool IsDeleted);
