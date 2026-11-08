using System.Linq.Expressions;
using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sport.Database.Queries;

public sealed class StatTypeQueries(ISportContext context) : ListingQueries<StatType, StatTypeListing>(context)
{
    protected override IQueryable<StatType> Set => Context.StatTypes;

    protected override Option<Expression<Func<StatType, string>>> Label => LabelledBy(statType => statType.Name);

    protected override IOrderedQueryable<StatType> Order(IQueryable<StatType> rows) =>
        rows.OrderBy(statType => statType.Scope)
            .ThenBy(statType => statType.Name.ToLower())
            .ThenBy(statType => EF.Property<Guid>(statType, VersionedColumns.Key));

    protected override IQueryable<StatType> Narrow(IQueryable<StatType> rows, SportFilter filter, Option<string> term) =>
        rows.Narrowed(filter.Discipline, id => statType => statType.DisciplineId == id)
            .Narrowed(filter.Scope, scope => statType => statType.Scope == scope)
            .Narrowed(
                term,
                text => statType => statType.Name.ToLower().Contains(text)
                                    || statType.Abbreviation.ToLower().Contains(text));

    protected override async Task<IReadOnlyList<StatTypeListing>> Present(
        IReadOnlyList<Keyed<StatType>> rows,
        DeletionState state,
        CancellationToken cancellationToken)
    {
        var disciplines = await Lookups.Disciplines(rows.Select(row => row.Entity.DisciplineId), cancellationToken);

        return rows
            .Select(row => new StatTypeListing(
                row.Key,
                row.Entity.DisciplineId,
                disciplines.Find(row.Entity.DisciplineId)?.Name ?? string.Empty,
                row.Entity.Name,
                row.Entity.Abbreviation,
                row.Entity.Scope,
                row.Entity.HigherIsBetter,
                state == DeletionState.Deleted))
            .ToList();
    }
}
