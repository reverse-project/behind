using System.Linq.Expressions;
using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Sport.Database.Queries;

public sealed class PlayerQueries(ISportContext context, TimeProvider time)
    : ListingQueries<Player, PlayerListing>(context)
{
    protected override IQueryable<Player> Set => Context.Players;

    protected override Option<Expression<Func<Player, string>>> Label => LabelledBy(player => player.LastName);

    protected override IOrderedQueryable<Player> Order(IQueryable<Player> rows) =>
        rows.OrderBy(player => player.LastName.ToLower())
            .ThenBy(player => player.FirstName.ToLower())
            .ThenBy(player => EF.Property<Guid>(player, VersionedColumns.Key));

    protected override IQueryable<Player> Narrow(IQueryable<Player> rows, SportFilter filter, Option<string> term)
    {
        var memberships = Context.ActiveLatest(Context.PlayerSeasons)
            .Narrowed(filter.Team, team => membership => membership.TeamId == team)
            .Narrowed(filter.Season, season => membership => membership.SeasonId == season);
        var scoped = !filter.Team.IsNone() || !filter.Season.IsNone();

        return rows
            .Narrowed(filter.Discipline, id => player => player.DisciplineId == id)
            .Narrowed(filter.Position, id => player => player.PositionId == id)
            .Narrowed(
                scoped ? Some(true) : None,
                _ => player => memberships.Any(membership =>
                    membership.PlayerId == EF.Property<Guid>(player, VersionedColumns.Key)))
            .Narrowed(
                term,
                text => player => (player.FirstName + " " + player.LastName).ToLower().Contains(text)
                                  || (player.LastName + " " + player.FirstName).ToLower().Contains(text)
                                  || (player.Nationality != null && player.Nationality.ToLower().Contains(text)));
    }

    protected override async Task<IReadOnlyList<PlayerListing>> Present(
        IReadOnlyList<Keyed<Player>> rows,
        DeletionState state,
        CancellationToken cancellationToken)
    {
        var keys = rows.Select(row => row.Key).ToList();
        var memberships = await Context.ActiveLatest(Context.PlayerSeasons)
            .Where(membership => keys.Contains(membership.PlayerId))
            .ToKeyedAsync(cancellationToken);

        var disciplines = await Lookups.Disciplines(rows.Select(row => row.Entity.DisciplineId), cancellationToken);
        var positions = await Lookups.Positions(
            rows.Select(row => row.Entity.PositionId).OfType<Guid>(),
            cancellationToken);
        var seasons = await Lookups.Seasons(memberships.Select(row => row.Entity.SeasonId), cancellationToken);
        var teams = await Lookups.Teams(memberships.Select(row => row.Entity.TeamId), cancellationToken);
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

        var recent = memberships
            .GroupBy(membership => membership.Entity.PlayerId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(membership => seasons.Find(membership.Entity.SeasonId)?.Recency ?? DateOnly.MinValue)
                    .ThenByDescending(membership => membership.Key)
                    .Select(membership => membership.Entity)
                    .ToList());

        return rows
            .Select(row => Listing(row, state, today, disciplines, positions, teams, recent.GetValueOrDefault(row.Key) ?? []))
            .ToList();
    }

    private static PlayerListing Listing(
        Keyed<Player> row,
        DeletionState state,
        DateOnly today,
        IReadOnlyDictionary<Guid, Discipline> disciplines,
        IReadOnlyDictionary<Guid, Position> positions,
        IReadOnlyDictionary<Guid, Team> teams,
        IReadOnlyList<PlayerSeason> memberships)
    {
        var player = row.Entity;
        var current = memberships.FirstOrDefault();

        return new PlayerListing(
            row.Key,
            player.DisciplineId,
            disciplines.Find(player.DisciplineId)?.Name ?? string.Empty,
            player.FirstName,
            player.LastName,
            player.BirthDate,
            player.BirthDate?.AgeOn(today),
            player.Nationality,
            player.PositionId,
            positions.Find(player.PositionId)?.Name,
            player.HeightCm,
            player.WeightKg,
            player.DominantSide,
            player.Biography,
            current?.TeamId,
            teams.Find(current?.TeamId)?.Name,
            memberships.Select(membership => membership.PictureId).OfType<Guid>().Select(SportFileTypes.PlayerImage).FirstOrDefault(),
            state == DeletionState.Deleted);
    }
}
