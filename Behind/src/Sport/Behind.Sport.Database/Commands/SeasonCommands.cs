using Behind.Common;
using Behind.Sport.Domain;

namespace Behind.Sport.Database.Commands;

public sealed class SeasonCommands(ISportContext context) : SportCommands<Season, SeasonDraft>(context)
{
    protected override Season Build(SeasonDraft draft) =>
        new(draft.DisciplineId, draft.Name, draft.StartsOn, draft.EndsOn);

    protected override Task<Option<string>> Validate(
        Guid key,
        SeasonDraft draft,
        CancellationToken cancellationToken) =>
        FirstRejection(
            async () => Reject(
                (await Active(Sport.Disciplines, draft.DisciplineId, cancellationToken)).IsNone(),
                SportRejections.UnknownReference),
            () => Task.FromResult(Reject(
                draft.StartsOn is { } starts && draft.EndsOn is { } ends && ends < starts,
                SportRejections.InvalidRange)));

    protected override async Task<bool> IsReferenced(Guid key, CancellationToken cancellationToken) =>
        await IsReferencedBy(Sport.PlayerSeasons, membership => membership.SeasonId == key, cancellationToken)
        || await IsReferencedBy(Sport.Stats, stat => stat.SeasonId == key, cancellationToken);
}
