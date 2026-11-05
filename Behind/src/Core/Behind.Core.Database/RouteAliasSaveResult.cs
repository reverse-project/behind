using Behind.Common;

namespace Behind.Core.Database;

public abstract record RouteAliasSaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : RouteAliasSaveResult;

    public sealed record NotFound : RouteAliasSaveResult;

    public sealed record AliasTaken : RouteAliasSaveResult;

    public sealed record VersionConflict : RouteAliasSaveResult;
}
