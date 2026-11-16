using Behind.Common.Database;
using Behind.Sport.Domain;

namespace Behind.Sport.Database;

public sealed record SubjectLabel(string Name, string? FirstName, string? LastName);

public sealed class SportLookups(ISportContext context)
{
    public Task<IReadOnlyDictionary<Guid, Discipline>> Disciplines(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default) =>
        Latest(context.Disciplines, ids, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, Position>> Positions(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default) =>
        Latest(context.Positions, ids, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, StatType>> StatTypes(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default) =>
        Latest(context.StatTypes, ids, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, Season>> Seasons(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default) =>
        Latest(context.Seasons, ids, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, Team>> Teams(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default) =>
        Latest(context.Teams, ids, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, Player>> Players(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default) =>
        Latest(context.Players, ids, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, SubjectLabel>> SubjectNames(
        IEnumerable<(Guid Id, StatScope Scope)> subjects,
        CancellationToken cancellationToken = default)
    {
        var listed = subjects.Distinct().ToList();
        var players = await Players(listed.Where(subject => subject.Scope == StatScope.Player).Select(subject => subject.Id), cancellationToken);
        var teams = await Teams(listed.Where(subject => subject.Scope == StatScope.Team).Select(subject => subject.Id), cancellationToken);

        return players
            .ToDictionary(
                player => player.Key,
                player => new SubjectLabel(player.Value.FullName, player.Value.FirstName, player.Value.LastName))
            .Concat(teams.ToDictionary(team => team.Key, team => new SubjectLabel(team.Value.Name, null, null)))
            .ToDictionary(label => label.Key, label => label.Value);
    }

    public async Task<IReadOnlyDictionary<Guid, TEntity>> Latest<TEntity>(
        IQueryable<TEntity> set,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var keys = ids.Distinct().ToList();

        if (keys.Count == 0)
        {
            return new Dictionary<Guid, TEntity>();
        }

        using var scope = context.SoftDelete.IncludeDeleted();

        var rows = await set.OfKeys(keys).ToKeyedAsync(cancellationToken);

        return rows.Latest().ToDictionary(row => row.Key, row => row.Entity);
    }
}
