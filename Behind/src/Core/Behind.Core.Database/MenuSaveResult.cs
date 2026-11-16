using Behind.Common;

namespace Behind.Core.Database;

public abstract record MenuSaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : MenuSaveResult;

    public sealed record NotFound : MenuSaveResult;

    public sealed record NameTaken : MenuSaveResult;

    public sealed record BuiltIn : MenuSaveResult;

    public sealed record VersionConflict : MenuSaveResult;
}

public enum MenuRemovalResult
{
    Removed,
    NotFound,
    BuiltIn
}
