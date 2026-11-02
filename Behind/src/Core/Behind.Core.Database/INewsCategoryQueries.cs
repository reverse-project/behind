using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface INewsCategoryQueries
{
    public sealed record NewsCategoryListing(
        VersionedKey<Guid> Id,
        string Name,
        Option<Guid> HeaderImageId);

    Task<PagedResult<NewsCategoryListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NewsCategoryListing>> ListActive(CancellationToken cancellationToken = default);
}
