using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IArticleQueries
{
    public sealed record ArticleCategoryInfo(Guid Id, string Name, Option<Guid> HeaderImageId);

    public sealed record ArticleListing(
        VersionedKey<Guid> Id,
        string Title,
        string Summary,
        string Author,
        Option<DateTimeOffset> CreatedAt,
        Option<DateTimeOffset> UpdatedAt,
        Option<ArticleCategoryInfo> Category)
    {
        public Guid AuthorId { get; init; }
    }

    public sealed record ManagedArticleListing(
        Guid Id,
        long LatestVersion,
        Option<long> ActiveVersion,
        string Title,
        string Author,
        Option<DateTimeOffset> CreatedAt,
        Option<DateTimeOffset> UpdatedAt,
        Option<ArticleCategoryInfo> Category)
    {
        public Guid AuthorId { get; init; }
    }

    public sealed record ArticleDetail(
        VersionedKey<Guid> Id,
        string Title,
        string Summary,
        string Content,
        string Author,
        Option<DateTimeOffset> CreatedAt,
        Option<DateTimeOffset> UpdatedAt,
        Option<ArticleCategoryInfo> Category)
    {
        public Guid AuthorId { get; init; }
        public string DisplayContent => string.IsNullOrWhiteSpace(Content) ? Summary : Content;
    }

    public sealed record ArticleVersionInfo(
        long Version,
        string Title,
        Option<DateTimeOffset> CreatedAt,
        bool IsActive);

    public sealed record ArticleHistory(Guid Id, Option<long> ActiveVersion, IReadOnlyList<ArticleVersionInfo> Versions);

    Task<PagedResult<ArticleListing>> List(
        Flipper flipper,
        Option<Guid> category,
        Option<string> search,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ManagedArticleListing>> ListManaged(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<Option<ArticleDetail>> Find(Guid id, CancellationToken cancellationToken = default);

    Task<Option<ArticleHistory>> FindHistory(Guid id, CancellationToken cancellationToken = default);

    Task<Option<ArticleDetail>> FindVersion(VersionedKey<Guid> id, CancellationToken cancellationToken = default);
}
