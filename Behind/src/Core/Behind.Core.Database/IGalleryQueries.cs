using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface IGalleryQueries
{
    public sealed record GalleryItemInfo(Guid Id, string Description, Guid ImageId, Guid ThumbnailId, int Position);

    public sealed record GalleryListing(
        VersionedKey<Guid> Id,
        string Title,
        string Description,
        string Author,
        Option<DateTimeOffset> CreatedAt,
        Option<DateTimeOffset> UpdatedAt,
        Option<Guid> ThumbnailId,
        int ItemCount)
    {
        public Guid AuthorId { get; init; }
    }

    public sealed record ManagedGalleryListing(
        Guid Id,
        long LatestVersion,
        Option<long> ActiveVersion,
        string Title,
        string Author,
        Option<DateTimeOffset> CreatedAt,
        Option<DateTimeOffset> UpdatedAt,
        Option<Guid> ThumbnailId,
        int ItemCount)
    {
        public Guid AuthorId { get; init; }
    }

    public sealed record GalleryDetail(
        VersionedKey<Guid> Id,
        string Title,
        string Description,
        string Author,
        Option<DateTimeOffset> CreatedAt,
        Option<DateTimeOffset> UpdatedAt,
        Option<Guid> ThumbnailId,
        bool ThumbnailGenerated,
        IReadOnlyList<GalleryItemInfo> Items)
    {
        public Guid AuthorId { get; init; }
    }

    public sealed record GalleryVersionInfo(
        long Version,
        string Title,
        int ItemCount,
        Option<DateTimeOffset> CreatedAt,
        bool IsActive);

    public sealed record GalleryHistory(Guid Id, Option<long> ActiveVersion, IReadOnlyList<GalleryVersionInfo> Versions);

    Task<PagedResult<GalleryListing>> List(
        Flipper flipper,
        Option<string> search,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ManagedGalleryListing>> ListManaged(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default);

    Task<Option<GalleryDetail>> Find(Guid id, CancellationToken cancellationToken = default);

    Task<Option<GalleryHistory>> FindHistory(Guid id, CancellationToken cancellationToken = default);

    Task<Option<GalleryDetail>> FindVersion(VersionedKey<Guid> id, CancellationToken cancellationToken = default);
}
