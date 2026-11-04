namespace Behind.Common.Database.Raw;

public sealed record RawTable(string Name, string? Schema, string EntityName, IReadOnlyList<RawColumn> Columns)
{
    public IReadOnlyList<RawColumn> KeyColumns => [..Columns.Where(column => column.IsKey)];
}
