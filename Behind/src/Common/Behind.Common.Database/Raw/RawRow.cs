namespace Behind.Common.Database.Raw;

public sealed record RawRow(IReadOnlyDictionary<string, object?> Values);
