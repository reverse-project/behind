using Behind.Common;
using Behind.Sport.Domain;

namespace Behind.Sport.Database.Commands;

public sealed class PositionCommands(ISportContext context) : SportCommands<Position, PositionDraft>(context)
{
    protected override Position Build(PositionDraft draft) =>
        new(draft.DisciplineId, draft.Name, draft.Abbreviation, draft.SortOrder);

    protected override async Task<Option<string>> Validate(
        Guid key,
        PositionDraft draft,
        CancellationToken cancellationToken) =>
        Reject(
            (await Active(Sport.Disciplines, draft.DisciplineId, cancellationToken)).IsNone(),
            SportRejections.UnknownReference);

    protected override Task<bool> IsReferenced(Guid key, CancellationToken cancellationToken) =>
        IsReferencedBy(Sport.Players, player => player.PositionId == key, cancellationToken);
}
