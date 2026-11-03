using Behind.Common;

namespace Behind.Core.Database;

public abstract record GallerySaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : GallerySaveResult;

    public sealed record NotFound : GallerySaveResult;

    public sealed record UnknownImage : GallerySaveResult;

    public sealed record VersionConflict : GallerySaveResult;
}
