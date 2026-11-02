using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface INewsQueries
{
    public sealed record NewsCategoryInfo(Guid Id, string Name, Option<Guid> HeaderImageId);

    public sealed record NewsListing(
        VersionedKey<Guid> Id,
        string Title,
        string Summary,
        string Author,
        Option<DateTimeOffset> CreatedAt,
        Option<DateTimeOffset> UpdatedAt,
        Option<NewsCategoryInfo> Category,
        long CommentCount,
        bool IsHighlighted,
        Option<DateTimeOffset> StartAt,
        Option<DateTimeOffset> EndAt);

    public sealed record NewsLiveInfo(Option<DateTimeOffset> StartAt, Option<DateTimeOffset> EndAt, bool Highlighted);

    public sealed record ManagedNewsListing(
        Guid Id,
        long LatestVersion,
        Option<long> ActiveVersion,
        string Title,
        string Author,
        Option<DateTimeOffset> CreatedAt,
        Option<DateTimeOffset> UpdatedAt,
        Option<NewsCategoryInfo> Category,
        long CommentCount,
        Option<NewsLiveInfo> Live);

    public sealed record NewsDetail(
        VersionedKey<Guid> Id,
        string Title,
        string Summary,
        string Content,
        string Author,
        Option<DateTimeOffset> CreatedAt,
        Option<DateTimeOffset> UpdatedAt,
        Option<NewsCategoryInfo> Category,
        bool IsHighlighted,
        Option<DateTimeOffset> StartAt,
        Option<DateTimeOffset> EndAt)
    {
        public string DisplayContent => string.IsNullOrWhiteSpace(Content) ? Summary : Content;
    }

    public sealed record NewsVersionInfo(
        long Version,
        string Title,
        Option<DateTimeOffset> StartAt,
        Option<DateTimeOffset> EndAt,
        bool Highlighted,
        Option<DateTimeOffset> CreatedAt,
        bool IsActive);

    public sealed record NewsHistory(Guid Id, Option<long> ActiveVersion, IReadOnlyList<NewsVersionInfo> Versions);

    Task<PagedResult<NewsListing>> List(
        Flipper flipper,
        Option<Guid> category,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ManagedNewsListing>> ListManaged(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<Option<NewsDetail>> Find(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<Option<NewsHistory>> FindHistory(Guid id, CancellationToken cancellationToken = default);

    Task<Option<NewsDetail>> FindVersion(VersionedKey<Guid> id, CancellationToken cancellationToken = default);
}
