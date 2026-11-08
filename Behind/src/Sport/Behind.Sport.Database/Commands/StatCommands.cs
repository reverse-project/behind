using Behind.Common;
using Behind.Common.Database;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Sport.Database.Commands;

public sealed class StatCommands(ISportContext context) : SportCommands<Stat, StatDraft>(context)
{
    protected override Stat Build(StatDraft draft) =>
        new(draft.SeasonId, draft.StatTypeId, draft.SubjectId, draft.Value);

    protected override Task<Option<string>> Validate(Guid key, StatDraft draft, CancellationToken cancellationToken) =>
        FirstRejection(
            () => ReferenceRejection(draft, cancellationToken),
            async () => Reject(
                await Sport.ActiveLatest(Sport.Stats)
                    .AnyAsync(
                        stat => stat.SeasonId == draft.SeasonId
                                && stat.StatTypeId == draft.StatTypeId
                                && stat.SubjectId == draft.SubjectId
                                && EF.Property<Guid>(stat, VersionedColumns.Key) != key,
                        cancellationToken),
                SportRejections.Duplicate));

    private async Task<Option<string>> ReferenceRejection(StatDraft draft, CancellationToken cancellationToken)
    {
        var season = await Active(Sport.Seasons, draft.SeasonId, cancellationToken);
        var statType = await Active(Sport.StatTypes, draft.StatTypeId, cancellationToken);
        var subjectDiscipline = await statType.Match(
            () => Task.FromResult<Option<Guid>>(None),
            found => SubjectDiscipline(found.Scope, draft.SubjectId, cancellationToken));

        var disciplines =
            from s in season
            from t in statType
            from subject in subjectDiscipline
            select new[] { s.DisciplineId, t.DisciplineId, subject };

        return disciplines.Match(
            () => Some(SportRejections.UnknownReference),
            ids => Reject(ids.Distinct().Count() > 1, SportRejections.Mismatch));
    }

    private async Task<Option<Guid>> SubjectDiscipline(
        StatScope scope,
        Guid subjectId,
        CancellationToken cancellationToken) =>
        scope == StatScope.Player
            ? (await Active(Sport.Players, subjectId, cancellationToken)).Map(player => player.DisciplineId)
            : (await Active(Sport.Teams, subjectId, cancellationToken)).Map(team => team.DisciplineId);
}
