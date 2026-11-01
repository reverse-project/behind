using Behind.Common;
using Behind.Core.Domain;

namespace Behind.Core.Database;

public interface ISiteQueries
{
    public sealed record SiteListing(
        Guid Id,
        string Title,
        string Slug,
        long LatestVersion,
        Option<long> ActiveVersion,
        Option<string> ActiveSlug,
        DateTimeOffset UpdatedAt);

    public sealed record SiteVersionInfo(
        long Version,
        string Title,
        string Slug,
        Option<DateTimeOffset> PublishedAt,
        DateTimeOffset CreatedAt,
        bool IsActive);

    public sealed record SiteHistory(Guid Id, Option<long> ActiveVersion, IReadOnlyList<SiteVersionInfo> Versions);

    Task<IReadOnlyList<SiteListing>> List(CancellationToken cancellationToken = default);

    Task<Option<SiteHistory>> FindHistory(Guid siteId, CancellationToken cancellationToken = default);

    Task<Option<Site>> FindVersion(VersionedKey<Guid> id, CancellationToken cancellationToken = default);

    Task<Option<Site>> FindActiveBySlug(string slug, CancellationToken cancellationToken = default);
}
