using Behind.Common;
using Behind.Sport.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Sport.Database.Commands;

public sealed class PlayerCommands(ISportContext context) : SportCommands<Player, PlayerDraft>(context)
{
    protected override Player Build(PlayerDraft draft) =>
        new(
            draft.DisciplineId,
            draft.FirstName,
            draft.LastName,
            draft.BirthDate,
            draft.Nationality,
            draft.PositionId,
            draft.HeightCm,
            draft.WeightKg,
            draft.DominantSide,
            draft.Biography);

    protected override Task<Option<string>> Validate(
        Guid key,
        PlayerDraft draft,
        CancellationToken cancellationToken) =>
        FirstRejection(
            async () => Reject(
                (await Active(Sport.Disciplines, draft.DisciplineId, cancellationToken)).IsNone(),
                SportRejections.UnknownReference),
            () => PositionRejection(draft, cancellationToken));

    protected override async Task<bool> IsReferenced(Guid key, CancellationToken cancellationToken) =>
        await IsReferencedBy(Sport.PlayerSeasons, membership => membership.PlayerId == key, cancellationToken)
        || await IsReferencedBy(Sport.Stats, stat => stat.SubjectId == key, cancellationToken);

    private async Task<Option<string>> PositionRejection(PlayerDraft draft, CancellationToken cancellationToken)
    {
        if (draft.PositionId is null)
        {
            return None;
        }

        var position = await Active(Sport.Positions, draft.PositionId, cancellationToken);

        return position.Match(
            () => Reject(true, SportRejections.UnknownReference),
            found => Reject(found.DisciplineId != draft.DisciplineId, SportRejections.Mismatch));
    }
}
