namespace Behind.Common.Database.Raw;

public sealed record RawColumn(
    string Name,
    string StoreType,
    RawValueKind Kind,
    bool IsNullable,
    bool IsKey,
    bool IsSensitive);
