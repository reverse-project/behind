using System.Linq.Expressions;
using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sport.Database.Queries;

public sealed class PositionQueries(ISportContext context) : ListingQueries<Position, PositionListing>(context)
{
    protected override IQueryable<Position> Set => Context.Positions;

    protected override Option<Expression<Func<Position, string>>> Label => LabelledBy(position => position.Name);

    protected override IOrderedQueryable<Position> Order(IQueryable<Position> rows) =>
        rows.OrderBy(position => position.SortOrder)
            .ThenBy(position => position.Name.ToLower())
            .ThenBy(position => EF.Property<Guid>(position, VersionedColumns.Key));

    protected override IQueryable<Position> Narrow(IQueryable<Position> rows, SportFilter filter, Option<string> term) =>
        rows.Narrowed(filter.Discipline, id => position => position.DisciplineId == id)
            .Narrowed(
                term,
                text => position => position.Name.ToLower().Contains(text)
                                    || position.Abbreviation.ToLower().Contains(text));

    protected override async Task<IReadOnlyList<PositionListing>> Present(
        IReadOnlyList<Keyed<Position>> rows,
        DeletionState state,
        CancellationToken cancellationToken)
    {
        var disciplines = await Lookups.Disciplines(rows.Select(row => row.Entity.DisciplineId), cancellationToken);

        return rows
            .Select(row => new PositionListing(
                row.Key,
                row.Entity.DisciplineId,
                disciplines.Find(row.Entity.DisciplineId)?.Name ?? string.Empty,
                row.Entity.Name,
                row.Entity.Abbreviation,
                row.Entity.SortOrder,
                state == DeletionState.Deleted))
            .ToList();
    }
}
