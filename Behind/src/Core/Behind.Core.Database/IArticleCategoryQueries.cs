using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IArticleCategoryQueries
{
    public sealed record ArticleCategoryListing(
        VersionedKey<Guid> Id,
        string Name,
        Option<Guid> HeaderImageId);

    Task<PagedResult<ArticleCategoryListing>> List(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArticleCategoryListing>> ListActive(CancellationToken cancellationToken = default);
}
