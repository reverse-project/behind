namespace Behind.Common;

public sealed record Flipper(int Index, int Size)
{
    public int Skip => Index > 1 ? (Index - 1) * Size : 0;

    public long PageCount(long total) => (total - total % Size) / Size + total % Size > 0 ? 1 : 0;
    
    public IQueryable<T> Apply<T>(IQueryable<T> queryable) => queryable.Skip(Skip).Take(Size);
}