using Behind.Common;

namespace Behind.Core.Database;

public abstract record ArticleSaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : ArticleSaveResult;

    public sealed record NotFound : ArticleSaveResult;

    public sealed record UnknownCategory : ArticleSaveResult;

    public sealed record VersionConflict : ArticleSaveResult;
}
