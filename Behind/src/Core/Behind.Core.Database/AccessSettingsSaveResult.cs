using Behind.Common;

namespace Behind.Core.Database;

public abstract record AccessSettingsSaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : AccessSettingsSaveResult;

    public sealed record NotFound : AccessSettingsSaveResult;

    public sealed record UnknownRole : AccessSettingsSaveResult;

    public sealed record VersionConflict : AccessSettingsSaveResult;
}
