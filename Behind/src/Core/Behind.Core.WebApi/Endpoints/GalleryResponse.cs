using Behind.Common.WebApi;

namespace Behind.Core.WebApi.Endpoints;

public sealed record GalleryListItem(
    Guid Id,
    long Version,
    string Title,
    string Description,
    string Author,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ThumbnailUrl,
    int ItemCount)
{
    public Guid AuthorId { get; init; }
}

public sealed record ManagedGalleryItem(
    Guid Id,
    long LatestVersion,
    long? ActiveVersion,
    string Title,
    string Author,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ThumbnailUrl,
    int ItemCount,
    bool IsDeleted,
    string Status,
    bool HasNewerDraft)
{
    public Guid AuthorId { get; init; }
}

public sealed record PublicGalleryListResponse(PageResponse<GalleryListItem> Galleries);

public sealed record GalleryManageResponse(PageResponse<ManagedGalleryItem> Galleries, IReadOnlyList<string> Letters);

public sealed record GalleryImageItem(
    Guid Id,
    string Description,
    Guid ImageId,
    string ImageUrl,
    Guid ThumbnailId,
    string ThumbnailUrl,
    int Position);

public sealed record GalleryDetailResponse(
    Guid Id,
    long Version,
    string Title,
    string Description,
    string Author,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? ThumbnailImageId,
    string? ThumbnailUrl,
    bool ThumbnailGenerated,
    IReadOnlyList<GalleryImageItem> Items)
{
    public Guid AuthorId { get; init; }
}

public sealed record GalleryVersionItem(
    long Version,
    string Title,
    int ItemCount,
    DateTimeOffset? CreatedAt,
    bool IsActive);

public sealed record GalleryHistoryResponse(Guid Id, long? ActiveVersion, IReadOnlyList<GalleryVersionItem> Versions);

public sealed record GalleryUploadedImage(
    string FileName,
    Guid? ImageId,
    string? ImageUrl,
    Guid? ThumbnailId,
    string? ThumbnailUrl,
    string? Error);

public sealed record GalleryImageResponse(Guid Id, string Url);

public sealed record GalleryImageLimitResponse(long MaxBytes);

public sealed record GalleryUploadResponse(IReadOnlyList<GalleryUploadedImage> Images);
