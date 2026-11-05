using Behind.Common;

namespace Behind.Core.Domain;

public sealed record Carousel(VersionedKey<Guid> Id, int IntervalSeconds)
{
    public const string EntityType = nameof(Carousel);

    public Carousel(int intervalSeconds)
        : this(new VersionedKey<Guid>(Guid.Empty), intervalSeconds)
    {
    }
}

public sealed record CarouselSlide(VersionedKey<Guid> Id, string Caption, string LinkUrl)
{
    public const string EntityType = nameof(CarouselSlide);
    public const int MaxCaptionLength = 1_000;
    public const int MaxLinkUrlLength = 2_000;

    public CarouselSlide(string caption, string linkUrl)
        : this(new VersionedKey<Guid>(Guid.Empty), caption, linkUrl)
    {
    }
}
