using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sport.Database.Queries;

public sealed class PlayerSeasonQueries(ISportContext context) : ListingQueries<PlayerSeason, PlayerSeasonListing>(context)
{
    protected override IQueryable<PlayerSeason> Set => Context.PlayerSeasons;

    protected override IOrderedQueryable<PlayerSeason> Order(IQueryable<PlayerSeason> rows)
    {
        var players = Context.ActiveLatest(Context.Players);

        return rows
            .OrderBy(membership => players
                .Where(player => EF.Property<Guid>(player, VersionedColumns.Key) == membership.PlayerId)
                .Select(player => player.LastName.ToLower())
                .FirstOrDefault())
            .ThenBy(membership => players
                .Where(player => EF.Property<Guid>(player, VersionedColumns.Key) == membership.PlayerId)
                .Select(player => player.FirstName.ToLower())
                .FirstOrDefault())
            .ThenByDescending(membership => EF.Property<Guid>(membership, VersionedColumns.Key));
    }

    protected override IQueryable<PlayerSeason> Narrow(
        IQueryable<PlayerSeason> rows,
        SportFilter filter,
        Option<string> term)
    {
        var players = Context.ActiveLatest(Context.Players);

        return rows
            .Narrowed(filter.Player, id => membership => membership.PlayerId == id)
            .Narrowed(filter.Season, id => membership => membership.SeasonId == id)
            .Narrowed(filter.Team, id => membership => membership.TeamId == id)
            .Narrowed(
                term,
                text => membership => players.Any(player =>
                    EF.Property<Guid>(player, VersionedColumns.Key) == membership.PlayerId
                    && (player.FirstName + " " + player.LastName).ToLower().Contains(text)));
    }

    protected override async Task<IReadOnlyList<PlayerSeasonListing>> Present(
        IReadOnlyList<Keyed<PlayerSeason>> rows,
        DeletionState state,
        CancellationToken cancellationToken)
    {
        var players = await Lookups.Players(rows.Select(row => row.Entity.PlayerId), cancellationToken);
        var positions = await Lookups.Positions(
            players.Values.Select(player => player.PositionId).OfType<Guid>(),
            cancellationToken);
        var seasons = await Lookups.Seasons(rows.Select(row => row.Entity.SeasonId), cancellationToken);
        var teams = await Lookups.Teams(rows.Select(row => row.Entity.TeamId), cancellationToken);

        return rows
            .Select(row =>
            {
                var player = players.Find(row.Entity.PlayerId);

                return new PlayerSeasonListing(
                    row.Key,
                    row.Entity.PlayerId,
                    player?.FullName ?? string.Empty,
                    player?.PositionId,
                    positions.Find(player?.PositionId)?.Name,
                    row.Entity.SeasonId,
                    seasons.Find(row.Entity.SeasonId)?.Name ?? string.Empty,
                    row.Entity.TeamId,
                    teams.Find(row.Entity.TeamId)?.Name ?? string.Empty,
                    row.Entity.ShirtNumber,
                    row.Entity.PictureId,
                    state == DeletionState.Deleted);
            })
            .ToList();
    }
}
