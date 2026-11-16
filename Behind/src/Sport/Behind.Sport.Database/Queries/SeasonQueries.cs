using System.Linq.Expressions;
using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sport.Database.Queries;

public sealed class SeasonQueries(ISportContext context) : ListingQueries<Season, SeasonListing>(context)
{
    protected override IQueryable<Season> Set => Context.Seasons;

    protected override Option<Expression<Func<Season, string>>> Label => LabelledBy(season => season.Name);

    protected override IOrderedQueryable<Season> Order(IQueryable<Season> rows) =>
        rows.OrderByDescending(season => season.StartsOn)
            .ThenByDescending(season => season.Name.ToLower())
            .ThenBy(season => EF.Property<Guid>(season, VersionedColumns.Key));

    protected override IQueryable<Season> Narrow(IQueryable<Season> rows, SportFilter filter, Option<string> term) =>
        rows.Narrowed(filter.Discipline, id => season => season.DisciplineId == id)
            .Narrowed(term, text => season => season.Name.ToLower().Contains(text));

    protected override async Task<IReadOnlyList<SeasonListing>> Present(
        IReadOnlyList<Keyed<Season>> rows,
        DeletionState state,
        CancellationToken cancellationToken)
    {
        var disciplines = await Lookups.Disciplines(rows.Select(row => row.Entity.DisciplineId), cancellationToken);

        return rows
            .Select(row => new SeasonListing(
                row.Key,
                row.Entity.DisciplineId,
                disciplines.Find(row.Entity.DisciplineId)?.Name ?? string.Empty,
                row.Entity.Name,
                row.Entity.StartsOn,
                row.Entity.EndsOn,
                state == DeletionState.Deleted))
            .ToList();
    }
}
