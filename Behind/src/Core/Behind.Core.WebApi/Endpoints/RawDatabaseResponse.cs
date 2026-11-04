namespace Behind.Core.WebApi.Endpoints;

public sealed record RawColumnResponse(
    string Name,
    string StoreType,
    string Kind,
    bool IsNullable,
    bool IsKey,
    bool IsSensitive);

public sealed record RawTableResponse(
    string Name,
    string EntityName,
    bool IsReadOnly,
    long Rows,
    IReadOnlyList<RawColumnResponse> Columns);

public sealed record RawTablesResponse(IReadOnlyList<RawTableResponse> Tables);

public sealed record RawInvalidResponse(IReadOnlyList<string> Errors);

public sealed record RawConflictResponse(string Reason, string? Message);
