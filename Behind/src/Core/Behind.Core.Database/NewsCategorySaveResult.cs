using Behind.Common;

namespace Behind.Core.Database;

public abstract record NewsCategorySaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : NewsCategorySaveResult;

    public sealed record NotFound : NewsCategorySaveResult;

    public sealed record NameTaken : NewsCategorySaveResult;

    public sealed record UnknownImage : NewsCategorySaveResult;

    public sealed record VersionConflict : NewsCategorySaveResult;
}
