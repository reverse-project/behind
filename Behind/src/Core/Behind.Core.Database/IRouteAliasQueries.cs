using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IRouteAliasQueries
{
    public sealed record RouteAliasListing(VersionedKey<Guid> Id, string Alias, string Target);

    Task<PagedResult<RouteAliasListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<Option<string>> FindTarget(string alias, CancellationToken cancellationToken = default);
}
