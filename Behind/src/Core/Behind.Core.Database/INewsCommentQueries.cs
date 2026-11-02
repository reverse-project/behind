using Behind.Common;

namespace Behind.Core.Database;

public interface INewsCommentQueries
{
    public sealed record NewsCommentListing(
        VersionedKey<Guid> Id,
        Guid AuthorId,
        string Author,
        string Content,
        Option<DateTimeOffset> CreatedAt,
        Option<DateTimeOffset> UpdatedAt = default);

    Task<PagedResult<NewsCommentListing>> List(
        Guid newsId,
        Flipper flipper,
        Option<DateTimeOffset> activeAt = default,
        CancellationToken cancellationToken = default);

    Task<Option<Guid>> FindAuthor(Guid commentId, CancellationToken cancellationToken = default);
}
