using Behind.Common.WebApi;

namespace Behind.Core.WebApi.Endpoints;

public sealed record NewsCategoryItem(Guid Id, string Name, Guid? HeaderImageId, string? HeaderImageUrl, bool IsDeleted = false);

public sealed record NewsListItem(
    Guid Id,
    long Version,
    string Title,
    string Summary,
    string Author,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    NewsCategoryItem? Category,
    long CommentCount,
    bool IsHighlighted,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt);

public sealed record ManagedNewsItem(
    Guid Id,
    long LatestVersion,
    long? ActiveVersion,
    string Title,
    string Author,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    NewsCategoryItem? Category,
    long CommentCount,
    bool IsDeleted,
    bool IsHighlighted,
    string Status,
    bool HasNewerDraft,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt);

public sealed record PublicNewsListResponse(PageResponse<NewsListItem> News, IReadOnlyList<NewsCategoryItem> Categories);

public sealed record NewsManageResponse(PageResponse<ManagedNewsItem> News, IReadOnlyList<string> Letters);

public sealed record NewsCategoryManageResponse(PageResponse<NewsCategoryItem> Categories, IReadOnlyList<string> Letters);

public sealed record NewsDetailResponse(
    Guid Id,
    long Version,
    string Title,
    string Summary,
    string Content,
    string DisplayContent,
    string Author,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    NewsCategoryItem? Category,
    bool IsHighlighted,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt);

public sealed record NewsVersionItem(
    long Version,
    string Title,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    bool IsHighlighted,
    DateTimeOffset? CreatedAt,
    bool IsActive);

public sealed record NewsHistoryResponse(Guid Id, long? ActiveVersion, IReadOnlyList<NewsVersionItem> Versions);

public sealed record NewsCommentItem(
    Guid Id,
    string Author,
    string Content,
    DateTimeOffset? CreatedAt,
    bool CanDelete,
    bool CanEdit = false,
    DateTimeOffset? UpdatedAt = null);

public sealed record NewsSavedResponse(Guid Id, long Version);

public sealed record NewsConflictResponse(string Reason);
