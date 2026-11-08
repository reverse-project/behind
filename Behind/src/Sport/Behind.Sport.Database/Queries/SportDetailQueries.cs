using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Sport.Database.Queries;

public sealed class SportDetailQueries(
    ISportContext context,
    IListingQueries<DisciplineListing> disciplines,
    IListingQueries<PositionListing> positions,
    IListingQueries<StatTypeListing> statTypes,
    IListingQueries<SeasonListing> seasons,
    IListingQueries<TeamListing> teams,
    IListingQueries<PlayerListing> players,
    IListingQueries<PlayerSeasonListing> memberships,
    IListingQueries<StatListing> stats) : ISportDetailQueries
{
    public const int MaxItems = 500;

    private readonly SportLookups _lookups = new(context);

    public Task<Option<DisciplineDetail>> Discipline(Guid id, CancellationToken cancellationToken = default) =>
        disciplines.Find(id, cancellationToken).MapAsync(async discipline =>
        {
            var filter = SportFilter.Empty with { Discipline = id };

            return new DisciplineDetail(
                discipline,
                await Items(positions, filter, cancellationToken),
                await Items(statTypes, filter, cancellationToken),
                await Items(seasons, filter, cancellationToken));
        });

    public Task<Option<TeamDetail>> Team(Guid id, CancellationToken cancellationToken = default) =>
        teams.Find(id, cancellationToken).MapAsync(async team =>
        {
            var roster = await Items(memberships, SportFilter.Empty with { Team = id }, cancellationToken);
            var teamStats = await Items(stats, SportFilter.Empty with { Team = id }, cancellationToken);
            var refs = await SeasonRefs(roster.Select(row => row.SeasonId).Concat(teamStats.Select(row => row.SeasonId)), cancellationToken);

            return new TeamDetail(
                team,
                refs.Select(season => new TeamSeason(
                        season,
                        roster.Where(row => row.SeasonId == season.Id).ToList(),
                        teamStats.Where(row => row.SeasonId == season.Id).ToList()))
                    .ToList());
        });

    public Task<Option<PlayerDetail>> Player(Guid id, CancellationToken cancellationToken = default) =>
        players.Find(id, cancellationToken).MapAsync(async player =>
        {
            var history = await Items(memberships, SportFilter.Empty with { Player = id }, cancellationToken);
            var playerStats = await Items(stats, SportFilter.Empty with { Player = id }, cancellationToken);
            var refs = await SeasonRefs(history.Select(row => row.SeasonId).Concat(playerStats.Select(row => row.SeasonId)), cancellationToken);

            return new PlayerDetail(
                player,
                refs.Select(season => new PlayerSeasonDetail(
                        season,
                        history.FirstOrDefault(row => row.SeasonId == season.Id),
                        playerStats.Where(row => row.SeasonId == season.Id).ToList()))
                    .ToList());
        });

    public Task<Option<SeasonDetail>> Season(Guid id, CancellationToken cancellationToken = default) =>
        seasons.Find(id, cancellationToken).MapAsync(async season =>
        {
            var roster = await Items(memberships, SportFilter.Empty with { Season = id }, cancellationToken);
            var found = await _lookups.Teams(roster.Select(row => row.TeamId), cancellationToken);

            return new SeasonDetail(
                season,
                roster.GroupBy(row => row.TeamId)
                    .Select(group => new SeasonTeam(
                        group.Key,
                        group.First().TeamName,
                        found.Find(group.Key)?.LogoId is { } logo ? SportFileTypes.TeamImage(logo) : null,
                        group.Count()))
                    .OrderBy(team => team.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList());
        });

    public Task<Option<SeasonLeaderboards>> Leaderboards(
        Guid seasonId,
        int limit,
        CancellationToken cancellationToken = default) =>
        seasons.Find(seasonId, cancellationToken).MapAsync(async season =>
        {
            var types = await Items(statTypes, SportFilter.Empty with { Discipline = season.DisciplineId }, cancellationToken);
            var boards = new List<Leaderboard>();

            foreach (var statType in types)
            {
                boards.Add(await Board(season, statType, limit, cancellationToken));
            }

            return new SeasonLeaderboards(season, boards.Where(board => board.Entries.Count > 0).ToList());
        });

    public async Task<Option<Leaderboard>> Leaderboard(
        Guid seasonId,
        Guid statTypeId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var season = await seasons.Find(seasonId, cancellationToken);
        var statType = await statTypes.Find(statTypeId, cancellationToken);

        var found =
            from s in season
            from t in statType
            where s.DisciplineId == t.DisciplineId
            select (Season: s, StatType: t);

        return await found.MapAsync(pair => Board(pair.Season, pair.StatType, limit, cancellationToken));
    }

    private async Task<Leaderboard> Board(
        SeasonListing season,
        StatTypeListing statType,
        int limit,
        CancellationToken cancellationToken)
    {
        var matching = context.ActiveLatest(context.Stats)
            .Where(stat => stat.SeasonId == season.Id && stat.StatTypeId == statType.Id);

        var ordered = statType.HigherIsBetter
            ? matching.OrderByDescending(stat => stat.Value)
            : matching.OrderBy(stat => stat.Value);

        var rows = await ordered
            .ThenBy(stat => EF.Property<Guid>(stat, VersionedColumns.Key))
            .Take(limit)
            .ToKeyedAsync(cancellationToken);

        return new Leaderboard(season, statType, await Entries(season.Id, statType.Scope, rows, cancellationToken));
    }

    private async Task<IReadOnlyList<LeaderboardEntry>> Entries(
        Guid seasonId,
        StatScope scope,
        IReadOnlyList<Keyed<Stat>> rows,
        CancellationToken cancellationToken)
    {
        var subjectIds = rows.Select(row => row.Entity.SubjectId).ToList();
        var playersById = scope == StatScope.Player
            ? await _lookups.Players(subjectIds, cancellationToken)
            : new Dictionary<Guid, Player>();
        var teamsById = scope == StatScope.Team
            ? await _lookups.Teams(subjectIds, cancellationToken)
            : new Dictionary<Guid, Team>();
        var roster = scope == StatScope.Player
            ? (await context.ActiveLatest(context.PlayerSeasons)
                .Where(membership => membership.SeasonId == seasonId && subjectIds.Contains(membership.PlayerId))
                .ToKeyedAsync(cancellationToken))
            .Select(row => row.Entity)
            .GroupBy(membership => membership.PlayerId)
            .ToDictionary(group => group.Key, group => group.First())
            : [];
        var rosterTeams = await _lookups.Teams(roster.Values.Select(membership => membership.TeamId), cancellationToken);

        return rows
            .Select((row, index) => new LeaderboardEntry(
                rows.TakeWhile(other => other.Entity.Value != row.Entity.Value).Count() + 1,
                row.Entity.SubjectId,
                scope == StatScope.Player
                    ? playersById.Find(row.Entity.SubjectId)?.FullName ?? string.Empty
                    : teamsById.Find(row.Entity.SubjectId)?.Name ?? string.Empty,
                scope,
                roster.GetValueOrDefault(row.Entity.SubjectId)?.TeamId,
                rosterTeams.Find(roster.GetValueOrDefault(row.Entity.SubjectId)?.TeamId)?.Name,
                roster.GetValueOrDefault(row.Entity.SubjectId)?.PictureId is { } picture
                    ? SportFileTypes.PlayerImage(picture)
                    : null,
                teamsById.Find(row.Entity.SubjectId)?.LogoId is { } logo ? SportFileTypes.TeamImage(logo) : null,
                row.Entity.Value))
            .ToList();
    }

    private async Task<IReadOnlyList<SeasonRef>> SeasonRefs(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var found = await _lookups.Seasons(ids, cancellationToken);

        return found
            .Select(season => (Id: season.Key, Season: season.Value))
            .OrderByDescending(item => item.Season.Recency)
            .ThenByDescending(item => item.Season.Name)
            .Select(item => new SeasonRef(item.Id, item.Season.Name, item.Season.StartsOn, item.Season.EndsOn))
            .ToList();
    }

    private static async Task<IReadOnlyList<TListing>> Items<TListing>(
        IListingQueries<TListing> queries,
        SportFilter filter,
        CancellationToken cancellationToken) =>
        (await queries.List(
            new ListRequest(new Flipper(1, MaxItems), None, None, DeletionState.Active, filter),
            cancellationToken))
        .Results.Match(() => [], items => items.ToList());
}
