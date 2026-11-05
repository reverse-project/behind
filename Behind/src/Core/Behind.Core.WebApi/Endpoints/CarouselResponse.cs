namespace Behind.Core.WebApi.Endpoints;

public sealed record CarouselSettingsResponse(
    int MaxSlides,
    int MinIntervalSeconds,
    int MaxIntervalSeconds,
    int DefaultIntervalSeconds,
    int MaxCaptionLength,
    int MaxLinkUrlLength,
    long MaxImageBytes);

public sealed record CarouselSlideItem(
    Guid Id,
    Guid ImageId,
    string ImageUrl,
    string Caption,
    string LinkUrl,
    int Position);

public sealed record CarouselResponse(
    Guid SiteId,
    long Version,
    int IntervalSeconds,
    IReadOnlyList<CarouselSlideItem> Slides);

public sealed record CarouselSavedResponse(Guid SiteId, long Version);

public sealed record CarouselConflictResponse(string Reason);
