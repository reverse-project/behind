using Behind.Common;

namespace Behind.Core.Database;

public abstract record ArticleCategorySaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : ArticleCategorySaveResult;

    public sealed record NotFound : ArticleCategorySaveResult;

    public sealed record NameTaken : ArticleCategorySaveResult;

    public sealed record UnknownImage : ArticleCategorySaveResult;

    public sealed record VersionConflict : ArticleCategorySaveResult;
}
