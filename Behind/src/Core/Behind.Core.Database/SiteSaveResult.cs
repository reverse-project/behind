using Behind.Common;

namespace Behind.Core.Database;

public abstract record SiteSaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : SiteSaveResult;

    public sealed record NotFound : SiteSaveResult;

    public sealed record SlugTaken : SiteSaveResult;

    public sealed record VersionConflict : SiteSaveResult;
}
