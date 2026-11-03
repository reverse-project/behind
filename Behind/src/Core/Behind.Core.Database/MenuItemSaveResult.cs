using Behind.Common;

namespace Behind.Core.Database;

public abstract record MenuItemSaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : MenuItemSaveResult;

    public sealed record NotFound : MenuItemSaveResult;

    public sealed record UnknownMenu : MenuItemSaveResult;

    public sealed record InvalidParent : MenuItemSaveResult;

    public sealed record KindNotAllowed : MenuItemSaveResult;

    public sealed record UnknownRole : MenuItemSaveResult;

    public sealed record VersionConflict : MenuItemSaveResult;
}
