using Behind.Common;
using Behind.Common.Database.Modules;
using Behind.Sport.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Sport.Database.Commands;

public sealed class TeamCommands(ISportContext context, IModuleFiles files) : SportCommands<Team, TeamDraft>(context)
{
    protected override Team Build(TeamDraft draft) =>
        new(
            draft.DisciplineId,
            draft.Name,
            draft.ShortName,
            draft.City,
            draft.Country,
            draft.FoundedYear,
            draft.Description,
            draft.LogoId);

    protected override Task<Option<string>> Validate(Guid key, TeamDraft draft, CancellationToken cancellationToken) =>
        FirstRejection(
            async () => Reject(
                (await Active(Sport.Disciplines, draft.DisciplineId, cancellationToken)).IsNone(),
                SportRejections.UnknownReference),
            async () => Reject(
                draft.LogoId is { } logo && !await files.Exists(SportFileTypes.Teams, logo, cancellationToken),
                SportRejections.UnknownImage));

    protected override async Task<bool> IsReferenced(Guid key, CancellationToken cancellationToken) =>
        await IsReferencedBy(Sport.PlayerSeasons, membership => membership.TeamId == key, cancellationToken)
        || await IsReferencedBy(Sport.Stats, stat => stat.SubjectId == key, cancellationToken);

    protected override Task Saved(VersionedKey<Guid> id, TeamDraft draft, CancellationToken cancellationToken) =>
        files.Attach(SportFileTypes.Teams, id.Key, draft.LogoId.ToOption().AsEnumerable().ToList(), cancellationToken);
}
