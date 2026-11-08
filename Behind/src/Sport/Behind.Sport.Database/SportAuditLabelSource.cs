using Behind.Common;
using Behind.Common.Database.Modules;
using Behind.Sport.Domain;

namespace Behind.Sport.Database;

public sealed class SportAuditLabelSource(ISportContext context) : IAuditLabelSource
{
    private const string Separator = " - ";

    public async Task<IReadOnlyList<AuditLabel>> LabelsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var lookups = new SportLookups(context);

        var disciplines = await lookups.Disciplines(ids, cancellationToken);
        var positions = await lookups.Positions(ids, cancellationToken);
        var statTypes = await lookups.StatTypes(ids, cancellationToken);
        var seasons = await lookups.Seasons(ids, cancellationToken);
        var teams = await lookups.Teams(ids, cancellationToken);
        var players = await lookups.Players(ids, cancellationToken);
        var memberships = await lookups.Latest(context.PlayerSeasons, ids, cancellationToken);
        var stats = await lookups.Latest(context.Stats, ids, cancellationToken);

        var membershipPlayers = await lookups.Players(memberships.Values.Select(row => row.PlayerId), cancellationToken);
        var membershipSeasons = await lookups.Seasons(memberships.Values.Select(row => row.SeasonId), cancellationToken);
        var membershipTeams = await lookups.Teams(memberships.Values.Select(row => row.TeamId), cancellationToken);
        var statStatTypes = await lookups.StatTypes(stats.Values.Select(row => row.StatTypeId), cancellationToken);
        var statSeasons = await lookups.Seasons(stats.Values.Select(row => row.SeasonId), cancellationToken);
        var subjects = await lookups.SubjectNames(
            stats.Values.Select(row => (row.SubjectId, statStatTypes.Find(row.StatTypeId)?.Scope ?? StatScope.Player)),
            cancellationToken);

        return
        [
            ..disciplines.Select(row => new AuditLabel(row.Key, row.Value.Name)),
            ..positions.Select(row => new AuditLabel(row.Key, row.Value.Name)),
            ..statTypes.Select(row => new AuditLabel(row.Key, row.Value.Name)),
            ..seasons.Select(row => new AuditLabel(row.Key, row.Value.Name)),
            ..teams.Select(row => new AuditLabel(row.Key, row.Value.Name)),
            ..players.Select(row => new AuditLabel(row.Key, row.Value.FullName)),
            ..memberships.Select(row => new AuditLabel(
                row.Key,
                string.Join(
                    Separator,
                    membershipPlayers.Find(row.Value.PlayerId)?.FullName,
                    membershipSeasons.Find(row.Value.SeasonId)?.Name,
                    membershipTeams.Find(row.Value.TeamId)?.Name))),
            ..stats.Select(row => new AuditLabel(
                row.Key,
                string.Join(
                    Separator,
                    subjects.GetValueOrDefault(row.Value.SubjectId),
                    statStatTypes.Find(row.Value.StatTypeId)?.Name,
                    statSeasons.Find(row.Value.SeasonId)?.Name)))
        ];
    }
}
