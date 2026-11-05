using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class RouteAliasQueries(AbstractCoreContext context) : IRouteAliasQueries
{
    public async Task<PagedResult<IRouteAliasQueries.RouteAliasListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Matching(search, state).InBucket(alias => alias.Alias, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(matching.ToRows())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<IRouteAliasQueries.RouteAliasListing>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            rows.Count > 0
                ? Some<IEnumerable<IRouteAliasQueries.RouteAliasListing>>(rows.Select(ToListing).ToList())
                : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Matching(search, state).LettersAsync(alias => alias.Alias, cancellationToken);
    }

    public async Task<Option<string>> FindTarget(string alias, CancellationToken cancellationToken = default)
    {
        var targets = await context.LatestRouteAliasesIn(DeletionState.Active)
            .Where(candidate => candidate.Alias == alias)
            .Select(candidate => candidate.Target)
            .Take(1)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return targets.Head();
    }

    private static IRouteAliasQueries.RouteAliasListing ToListing(RouteAliasRow row) =>
        new(new VersionedKey<Guid>(row.Key, row.Version), row.Alias, row.Target);

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<RouteAlias> Matching(Option<string> search, DeletionState state)
    {
        var latest = context.LatestRouteAliasesIn(state);

        return search
            .Map(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .Match(
                () => latest,
                term => latest.Where(alias => alias.Alias.Contains(term) || alias.Target.ToLower().Contains(term)));
    }
}
