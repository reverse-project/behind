using System.Linq.Expressions;
using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sport.Database.Queries;

public sealed class TeamQueries(ISportContext context) : ListingQueries<Team, TeamListing>(context)
{
    protected override IQueryable<Team> Set => Context.Teams;

    protected override Option<Expression<Func<Team, string>>> Label => LabelledBy(team => team.Name);

    protected override IOrderedQueryable<Team> Order(IQueryable<Team> rows) =>
        rows.OrderBy(team => team.Name.ToLower())
            .ThenBy(team => EF.Property<Guid>(team, VersionedColumns.Key));

    protected override IQueryable<Team> Narrow(IQueryable<Team> rows, SportFilter filter, Option<string> term)
    {
        var memberships = Context.ActiveLatest(Context.PlayerSeasons);

        return rows
            .Narrowed(filter.Discipline, id => team => team.DisciplineId == id)
            .Narrowed(
                filter.Season,
                season => team => memberships.Any(membership =>
                    membership.SeasonId == season
                    && membership.TeamId == EF.Property<Guid>(team, VersionedColumns.Key)))
            .Narrowed(
                term,
                text => team => team.Name.ToLower().Contains(text)
                                || (team.ShortName != null && team.ShortName.ToLower().Contains(text))
                                || (team.City != null && team.City.ToLower().Contains(text))
                                || (team.Country != null && team.Country.ToLower().Contains(text)));
    }

    protected override async Task<IReadOnlyList<TeamListing>> Present(
        IReadOnlyList<Keyed<Team>> rows,
        DeletionState state,
        CancellationToken cancellationToken)
    {
        var disciplines = await Lookups.Disciplines(rows.Select(row => row.Entity.DisciplineId), cancellationToken);

        return rows
            .Select(row => new TeamListing(
                row.Key,
                row.Entity.DisciplineId,
                disciplines.Find(row.Entity.DisciplineId)?.Name ?? string.Empty,
                row.Entity.Name,
                row.Entity.ShortName,
                row.Entity.City,
                row.Entity.Country,
                row.Entity.FoundedYear,
                row.Entity.Description,
                row.Entity.LogoId,
                state == DeletionState.Deleted))
            .ToList();
    }
}
