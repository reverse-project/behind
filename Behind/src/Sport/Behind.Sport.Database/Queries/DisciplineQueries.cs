using System.Linq.Expressions;
using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sport.Database.Queries;

public sealed class DisciplineQueries(ISportContext context) : ListingQueries<Discipline, DisciplineListing>(context)
{
    protected override IQueryable<Discipline> Set => Context.Disciplines;

    protected override Option<Expression<Func<Discipline, string>>> Label => LabelledBy(discipline => discipline.Name);

    protected override IOrderedQueryable<Discipline> Order(IQueryable<Discipline> rows) =>
        rows.OrderBy(discipline => discipline.Name.ToLower())
            .ThenBy(discipline => EF.Property<Guid>(discipline, VersionedColumns.Key));

    protected override IQueryable<Discipline> Narrow(
        IQueryable<Discipline> rows,
        SportFilter filter,
        Option<string> term) =>
        rows.Narrowed(
            term,
            text => discipline => discipline.Name.ToLower().Contains(text) || discipline.Slug.Contains(text));

    protected override Task<IReadOnlyList<DisciplineListing>> Present(
        IReadOnlyList<Keyed<Discipline>> rows,
        DeletionState state,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DisciplineListing>>(
            rows.Select(row => new DisciplineListing(
                    row.Key,
                    row.Entity.Name,
                    row.Entity.Slug,
                    state == DeletionState.Deleted))
                .ToList());
}
