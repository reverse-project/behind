using Behind.Common.WebApi;

namespace Behind.Core.WebApi.Endpoints;

public sealed record ArticleListItem(
    Guid Id,
    long Version,
    string Title,
    string Summary,
    string Author,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    NewsCategoryItem? Category);

public sealed record ManagedArticleItem(
    Guid Id,
    long LatestVersion,
    long? ActiveVersion,
    string Title,
    string Author,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    NewsCategoryItem? Category,
    bool IsDeleted,
    string Status,
    bool HasNewerDraft);

public sealed record PublicArticleListResponse(
    PageResponse<ArticleListItem> Articles,
    IReadOnlyList<NewsCategoryItem> Categories);

public sealed record ArticleManageResponse(PageResponse<ManagedArticleItem> Articles, IReadOnlyList<string> Letters);

public sealed record ArticleCategoryManageResponse(
    PageResponse<NewsCategoryItem> Categories,
    IReadOnlyList<string> Letters);

public sealed record ArticleDetailResponse(
    Guid Id,
    long Version,
    string Title,
    string Summary,
    string Content,
    string DisplayContent,
    string Author,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    NewsCategoryItem? Category);

public sealed record ArticleVersionItem(long Version, string Title, DateTimeOffset? CreatedAt, bool IsActive);

public sealed record ArticleHistoryResponse(Guid Id, long? ActiveVersion, IReadOnlyList<ArticleVersionItem> Versions);
