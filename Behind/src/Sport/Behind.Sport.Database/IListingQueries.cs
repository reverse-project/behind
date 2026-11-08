using Behind.Common;

namespace Behind.Sport.Database;

public interface IListingQueries<TListing>
{
    Task<PagedResult<TListing>> List(ListRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(ListRequest request, CancellationToken cancellationToken = default);

    Task<Option<TListing>> Find(Guid id, CancellationToken cancellationToken = default);
}
