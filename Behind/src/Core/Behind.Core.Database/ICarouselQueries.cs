using Behind.Common;
using Behind.Common.Database;

namespace Behind.Core.Database;

public interface ICarouselQueries
{
    public sealed record CarouselSlideInfo(Guid Id, Guid ImageId, string Caption, string LinkUrl, int Position);

    public sealed record CarouselDetail(
        VersionedKey<Guid> Id,
        int IntervalSeconds,
        IReadOnlyList<CarouselSlideInfo> Slides);

    Task<Option<CarouselDetail>> FindLive(Guid siteId, CancellationToken cancellationToken = default);

    Task<Option<CarouselDetail>> FindManaged(
        Guid siteId,
        DeletionState state,
        CancellationToken cancellationToken = default);
}
