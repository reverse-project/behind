using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Sport.Database.Commands;

public sealed class StatTypeCommands(ISportContext context) : SportCommands<StatType, StatTypeDraft>(context)
{
    protected override StatType Build(StatTypeDraft draft) =>
        new(draft.DisciplineId, draft.Name, draft.Abbreviation, draft.Scope, draft.HigherIsBetter);

    protected override Task<Option<string>> Validate(
        Guid key,
        StatTypeDraft draft,
        CancellationToken cancellationToken) =>
        FirstRejection(
            async () => Reject(
                (await Active(Sport.Disciplines, draft.DisciplineId, cancellationToken)).IsNone(),
                SportRejections.UnknownReference),
            async () => Reject(
                await ScopeChangesUsedStats(key, draft, cancellationToken),
                SportRejections.Mismatch));

    protected override Task<bool> IsReferenced(Guid key, CancellationToken cancellationToken) =>
        IsReferencedBy(Sport.Stats, stat => stat.StatTypeId == key, cancellationToken);

    private async Task<bool> ScopeChangesUsedStats(
        Guid key,
        StatTypeDraft draft,
        CancellationToken cancellationToken)
    {
        var current = await Active(Sport.StatTypes, key, cancellationToken);

        return current.Match(() => false, existing => existing.Scope != draft.Scope)
               && await Sport.ActiveLatest(Sport.Stats).AnyAsync(stat => stat.StatTypeId == key, cancellationToken);
    }
}
