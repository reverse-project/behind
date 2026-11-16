using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Sport.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Sport.Database.Commands;

public sealed class PlayerSeasonCommands(ISportContext context, IModuleFiles files)
    : SportCommands<PlayerSeason, PlayerSeasonDraft>(context)
{
    protected override PlayerSeason Build(PlayerSeasonDraft draft) =>
        new(draft.PlayerId, draft.SeasonId, draft.TeamId, draft.ShirtNumber, draft.PictureId);

    protected override Task<Option<string>> Validate(
        Guid key,
        PlayerSeasonDraft draft,
        CancellationToken cancellationToken) =>
        FirstRejection(
            () => ReferenceRejection(draft, cancellationToken),
            async () => Reject(
                await Sport.ActiveLatest(Sport.PlayerSeasons)
                    .AnyAsync(
                        membership => membership.PlayerId == draft.PlayerId
                                      && membership.SeasonId == draft.SeasonId
                                      && EF.Property<Guid>(membership, VersionedColumns.Key) != key,
                        cancellationToken),
                SportRejections.Duplicate),
            async () => Reject(
                draft.PictureId is { } picture
                && !await files.Exists(SportFileTypes.Players, picture, cancellationToken),
                SportRejections.UnknownImage));

    protected override Task Saved(
        VersionedKey<Guid> id,
        PlayerSeasonDraft draft,
        CancellationToken cancellationToken) =>
        files.Attach(
            SportFileTypes.Players,
            draft.PlayerId,
            draft.PictureId.ToOption().AsEnumerable().ToList(),
            cancellationToken);

    private async Task<Option<string>> ReferenceRejection(
        PlayerSeasonDraft draft,
        CancellationToken cancellationToken)
    {
        var player = await Active(Sport.Players, draft.PlayerId, cancellationToken);
        var season = await Active(Sport.Seasons, draft.SeasonId, cancellationToken);
        var team = await Active(Sport.Teams, draft.TeamId, cancellationToken);

        var disciplines =
            from p in player
            from s in season
            from t in team
            select new[] { p.DisciplineId, s.DisciplineId, t.DisciplineId };

        return disciplines.Match(
            () => Some(SportRejections.UnknownReference),
            ids => Reject(ids.Distinct().Count() > 1, SportRejections.Mismatch));
    }
}
