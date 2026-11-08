using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sport.Database.Commands;

public sealed class DisciplineCommands(ISportContext context) : SportCommands<Discipline, DisciplineDraft>(context)
{
    protected override Discipline Build(DisciplineDraft draft) => new(draft.Name, draft.Slug);

    protected override async Task<Option<string>> Validate(
        Guid key,
        DisciplineDraft draft,
        CancellationToken cancellationToken) =>
        Reject(
            await Sport.ActiveLatest(Sport.Disciplines)
                .AnyAsync(
                    discipline => discipline.Slug == draft.Slug
                                  && EF.Property<Guid>(discipline, VersionedColumns.Key) != key,
                    cancellationToken),
            SportRejections.Duplicate);

    protected override async Task<bool> IsReferenced(Guid key, CancellationToken cancellationToken) =>
        await IsReferencedBy(Sport.Positions, row => row.DisciplineId == key, cancellationToken)
        || await IsReferencedBy(Sport.StatTypes, row => row.DisciplineId == key, cancellationToken)
        || await IsReferencedBy(Sport.Seasons, row => row.DisciplineId == key, cancellationToken)
        || await IsReferencedBy(Sport.Teams, row => row.DisciplineId == key, cancellationToken)
        || await IsReferencedBy(Sport.Players, row => row.DisciplineId == key, cancellationToken);
}
