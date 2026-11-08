using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Sport.Domain;

namespace Behind.Sport.Database;

public sealed record DisciplineDraft(
    [property: Required, MaxLength(Discipline.MaxNameLength)] string Name,
    [property: Required, MaxLength(Discipline.MaxSlugLength), RegularExpression(DisciplineDraft.SlugPattern)] string Slug)
    : IDraft<DisciplineDraft>
{
    public const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    public DisciplineDraft Normalized() => new(Name.ToRequiredText(), Slug.ToRequiredText().ToLowerInvariant());
}

public sealed record PositionDraft(
    Guid DisciplineId,
    [property: Required, MaxLength(Position.MaxNameLength)] string Name,
    [property: Required, MaxLength(Position.MaxAbbreviationLength)] string Abbreviation,
    int SortOrder) : IDraft<PositionDraft>
{
    public PositionDraft Normalized() => this with { Name = Name.ToRequiredText(), Abbreviation = Abbreviation.ToRequiredText() };
}

public sealed record StatTypeDraft(
    Guid DisciplineId,
    [property: Required, MaxLength(StatType.MaxNameLength)] string Name,
    [property: Required, MaxLength(StatType.MaxAbbreviationLength)] string Abbreviation,
    StatScope Scope,
    bool HigherIsBetter) : IDraft<StatTypeDraft>
{
    public StatTypeDraft Normalized() => this with { Name = Name.ToRequiredText(), Abbreviation = Abbreviation.ToRequiredText() };
}

public sealed record SeasonDraft(
    Guid DisciplineId,
    [property: Required, MaxLength(Season.MaxNameLength)] string Name,
    DateOnly? StartsOn,
    DateOnly? EndsOn) : IDraft<SeasonDraft>
{
    public SeasonDraft Normalized() => this with { Name = Name.ToRequiredText() };
}

public sealed record TeamDraft(
    Guid DisciplineId,
    [property: Required, MaxLength(Team.MaxNameLength)] string Name,
    [property: MaxLength(Team.MaxShortNameLength)] string? ShortName,
    [property: MaxLength(Team.MaxCityLength)] string? City,
    [property: MaxLength(Team.MaxCountryLength)] string? Country,
    [property: Range(Team.MinFoundedYear, Team.MaxFoundedYear)] int? FoundedYear,
    [property: MaxLength(Team.MaxDescriptionLength)] string? Description,
    Guid? LogoId) : IDraft<TeamDraft>
{
    public TeamDraft Normalized() =>
        this with
        {
            Name = Name.ToRequiredText(),
            ShortName = ShortName.ToOptionalText(),
            City = City.ToOptionalText(),
            Country = Country.ToOptionalText(),
            Description = Description.ToOptionalText()
        };
}

public sealed record PlayerDraft(
    Guid DisciplineId,
    [property: Required, MaxLength(Player.MaxNameLength)] string FirstName,
    [property: Required, MaxLength(Player.MaxNameLength)] string LastName,
    DateOnly? BirthDate,
    [property: MaxLength(Player.MaxNationalityLength)] string? Nationality,
    Guid? PositionId,
    [property: Range(Player.MinHeightCm, Player.MaxHeightCm)] int? HeightCm,
    [property: Range(Player.MinWeightKg, Player.MaxWeightKg)] int? WeightKg,
    Side? DominantSide,
    [property: MaxLength(Player.MaxBiographyLength)] string? Biography) : IDraft<PlayerDraft>
{
    public PlayerDraft Normalized() =>
        this with
        {
            FirstName = FirstName.ToRequiredText(),
            LastName = LastName.ToRequiredText(),
            Nationality = Nationality.ToOptionalText(),
            Biography = Biography.ToOptionalText()
        };
}

public sealed record PlayerSeasonDraft(
    Guid PlayerId,
    Guid SeasonId,
    Guid TeamId,
    [property: Range(PlayerSeason.MinShirtNumber, PlayerSeason.MaxShirtNumber)] int? ShirtNumber,
    Guid? PictureId) : IDraft<PlayerSeasonDraft>
{
    public PlayerSeasonDraft Normalized() => this;
}

public sealed record StatDraft(
    Guid SeasonId,
    Guid StatTypeId,
    Guid SubjectId,
    [property: Range(-Stat.MaxValue, Stat.MaxValue)] double Value) : IDraft<StatDraft>
{
    public StatDraft Normalized() => this;
}
