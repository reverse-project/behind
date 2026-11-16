using Behind.Common;

namespace Behind.Core.Database;

public abstract record CarouselSaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : CarouselSaveResult;

    public sealed record NotFound : CarouselSaveResult;

    public sealed record UnknownImage : CarouselSaveResult;

    public sealed record VersionConflict : CarouselSaveResult;
}
