using Behind.Common;

namespace Behind.Core.Database;

public abstract record NewsSaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : NewsSaveResult;

    public sealed record NotFound : NewsSaveResult;

    public sealed record UnknownCategory : NewsSaveResult;

    public sealed record InvalidSchedule : NewsSaveResult;

    public sealed record VersionConflict : NewsSaveResult;
}
