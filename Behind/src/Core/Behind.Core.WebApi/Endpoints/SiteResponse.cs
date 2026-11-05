using Behind.Common.WebApi;

namespace Behind.Core.WebApi.Endpoints;

public sealed record SiteListItem(
    Guid Id,
    string Title,
    string Slug,
    long LatestVersion,
    long? ActiveVersion,
    string? ActiveSlug,
    DateTimeOffset UpdatedAt,
    bool IsDeleted = false);

public sealed record SiteListResponse(PageResponse<SiteListItem> Sites, IReadOnlyList<string> Letters);

public sealed record SiteVersionItem(
    long Version,
    string Title,
    string Slug,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    bool IsActive,
    bool IsDraft);

public sealed record SiteHistoryResponse(Guid Id, long? ActiveVersion, IReadOnlyList<SiteVersionItem> Versions);

public sealed record SiteVersionResponse(
    Guid Id,
    long Version,
    string Title,
    string Slug,
    string Content,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    bool IsDraft);

public sealed record SiteSavedResponse(Guid Id, long Version);

public sealed record SiteConflictResponse(string Reason);

public sealed record PublicSiteResponse(Guid Id, string Title, string Slug, string Content, DateTimeOffset PublishedAt);

public sealed record SiteImageResponse(Guid Id, string Url);

public sealed record SiteSettingsResponse(string? IndexSlug, long MaxImageBytes);

public sealed record SiteImageLimitResponse(long MaxBytes);
