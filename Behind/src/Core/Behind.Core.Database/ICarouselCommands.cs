using Behind.Common.Database;

namespace Behind.Core.Database;

public interface ICarouselCommands
{
    public sealed record CarouselSlideDraft(Guid ImageId, string Caption, string LinkUrl);

    public sealed record CarouselDraft(int IntervalSeconds, IReadOnlyList<CarouselSlideDraft> Slides);

    Task<CarouselSaveResult> Save(Guid siteId, CarouselDraft draft, CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid siteId, CancellationToken cancellationToken = default);

    Task<bool> Restore(Guid siteId, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid siteId, CancellationToken cancellationToken = default);
}
