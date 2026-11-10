using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sport.Database.Queries;

public sealed class StatQueries(ISportContext context) : ListingQueries<Stat, StatListing>(context)
{
    protected override IQueryable<Stat> Set => Context.Stats;

    protected override IOrderedQueryable<Stat> Order(IQueryable<Stat> rows) =>
        rows.OrderByDescending(stat => stat.Value)
            .ThenBy(stat => EF.Property<Guid>(stat, VersionedColumns.Key));

    protected override IQueryable<Stat> Narrow(IQueryable<Stat> rows, SportFilter filter, Option<string> term)
    {
        var statTypes = Context.ActiveLatest(Context.StatTypes);
        var players = Context.ActiveLatest(Context.Players);
        var teams = Context.ActiveLatest(Context.Teams);

        return rows
            .Narrowed(filter.Season, id => stat => stat.SeasonId == id)
            .Narrowed(filter.StatType, id => stat => stat.StatTypeId == id)
            .Narrowed(filter.Player, id => stat => stat.SubjectId == id)
            .Narrowed(filter.Team, id => stat => stat.SubjectId == id)
            .Narrowed(
                filter.Scope,
                scope => stat => statTypes.Any(statType =>
                    EF.Property<Guid>(statType, VersionedColumns.Key) == stat.StatTypeId && statType.Scope == scope))
            .Narrowed(
                filter.Discipline,
                id => stat => statTypes.Any(statType =>
                    EF.Property<Guid>(statType, VersionedColumns.Key) == stat.StatTypeId && statType.DisciplineId == id))
            .Narrowed(
                term,
                text => stat => statTypes.Any(statType =>
                                    EF.Property<Guid>(statType, VersionedColumns.Key) == stat.StatTypeId
                                    && statType.Name.ToLower().Contains(text))
                                || players.Any(player =>
                                    EF.Property<Guid>(player, VersionedColumns.Key) == stat.SubjectId
                                    && ((player.FirstName + " " + player.LastName).ToLower().Contains(text)
                                        || (player.LastName + " " + player.FirstName).ToLower().Contains(text)))
                                || teams.Any(team =>
                                    EF.Property<Guid>(team, VersionedColumns.Key) == stat.SubjectId
                                    && team.Name.ToLower().Contains(text)));
    }

    protected override async Task<IReadOnlyList<StatListing>> Present(
        IReadOnlyList<Keyed<Stat>> rows,
        DeletionState state,
        CancellationToken cancellationToken)
    {
        var statTypes = await Lookups.StatTypes(rows.Select(row => row.Entity.StatTypeId), cancellationToken);
        var seasons = await Lookups.Seasons(rows.Select(row => row.Entity.SeasonId), cancellationToken);
        var subjects = await Lookups.SubjectNames(
            rows.Select(row => (row.Entity.SubjectId, statTypes.Find(row.Entity.StatTypeId)?.Scope ?? StatScope.Player)),
            cancellationToken);

        return rows
            .Select(row =>
            {
                var statType = statTypes.Find(row.Entity.StatTypeId);

                return new StatListing(
                    row.Key,
                    row.Entity.SeasonId,
                    seasons.Find(row.Entity.SeasonId)?.Name ?? string.Empty,
                    row.Entity.StatTypeId,
                    statType?.Name ?? string.Empty,
                    statType?.Abbreviation ?? string.Empty,
                    statType?.Scope ?? StatScope.Player,
                    row.Entity.SubjectId,
                    subjects.GetValueOrDefault(row.Entity.SubjectId)?.Name ?? string.Empty,
                    subjects.GetValueOrDefault(row.Entity.SubjectId)?.FirstName,
                    subjects.GetValueOrDefault(row.Entity.SubjectId)?.LastName,
                    row.Entity.Value,
                    state == DeletionState.Deleted);
            })
            .ToList();
    }
}
