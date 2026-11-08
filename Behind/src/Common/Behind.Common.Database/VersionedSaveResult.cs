namespace Behind.Common.Database;

public abstract record VersionedSaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : VersionedSaveResult;

    public sealed record NotFound : VersionedSaveResult;

    public sealed record VersionConflict : VersionedSaveResult;

    public sealed record Rejected(string Reason) : VersionedSaveResult;
}
