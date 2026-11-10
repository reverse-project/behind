using Behind.Common;
using Behind.Sport.Domain;

namespace Behind.Sport.Database;

public sealed record SeasonRef(Guid Id, string Name, DateOnly? StartsOn, DateOnly? EndsOn);

public sealed record TeamSeason(
    SeasonRef Season,
    IReadOnlyList<PlayerSeasonListing> Players,
    IReadOnlyList<StatListing> Stats);

public sealed record TeamDetail(TeamListing Team, IReadOnlyList<TeamSeason> Seasons);

public sealed record PlayerSeasonDetail(
    SeasonRef Season,
    PlayerSeasonListing? Membership,
    IReadOnlyList<StatListing> Stats);

public sealed record PlayerDetail(PlayerListing Player, IReadOnlyList<PlayerSeasonDetail> Seasons);

public sealed record SeasonTeam(Guid Id, string Name, string? LogoUrl, int PlayerCount);

public sealed record SeasonDetail(SeasonListing Season, IReadOnlyList<SeasonTeam> Teams);

public sealed record DisciplineDetail(
    DisciplineListing Discipline,
    IReadOnlyList<PositionListing> Positions,
    IReadOnlyList<StatTypeListing> StatTypes,
    IReadOnlyList<SeasonListing> Seasons);

public sealed record LeaderboardEntry(
    int Rank,
    Guid SubjectId,
    string SubjectName,
    string? SubjectFirstName,
    string? SubjectLastName,
    StatScope Scope,
    Guid? TeamId,
    string? TeamName,
    string? PictureUrl,
    string? LogoUrl,
    double Value);

public sealed record Leaderboard(SeasonListing Season, StatTypeListing StatType, IReadOnlyList<LeaderboardEntry> Entries);

public sealed record SeasonLeaderboards(SeasonListing Season, IReadOnlyList<Leaderboard> Boards);

public interface ISportDetailQueries
{
    Task<Option<DisciplineDetail>> Discipline(Guid id, CancellationToken cancellationToken = default);

    Task<Option<TeamDetail>> Team(Guid id, CancellationToken cancellationToken = default);

    Task<Option<PlayerDetail>> Player(Guid id, CancellationToken cancellationToken = default);

    Task<Option<SeasonDetail>> Season(Guid id, CancellationToken cancellationToken = default);

    Task<Option<SeasonLeaderboards>> Leaderboards(Guid seasonId, int limit, CancellationToken cancellationToken = default);

    Task<Option<Leaderboard>> Leaderboard(
        Guid seasonId,
        Guid statTypeId,
        int limit,
        CancellationToken cancellationToken = default);
}
