namespace Behind.Common;

public enum Direction
{
    Ascending, 
    Descending
}

public sealed record OrderBy(string FieldName, Direction Direction = Direction.Ascending);