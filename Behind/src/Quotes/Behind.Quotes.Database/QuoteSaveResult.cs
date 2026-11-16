using Behind.Common;

namespace Behind.Quotes.Database;

public abstract record QuoteSaveResult
{
    public sealed record Saved(VersionedKey<Guid> Id) : QuoteSaveResult;

    public sealed record NotFound : QuoteSaveResult;

    public sealed record VersionConflict : QuoteSaveResult;
}
