namespace Behind.Common;

public sealed record Flipper(int Index, int Size)
{
    public int Skip => Index > 1 ? (Index - 1) * Size : 0;

    public long PageCount(long total) => total <= 0 || Size <= 0 ? 0 : (total + Size - 1) / Size;

    public IQueryable<T> Apply<T>(IQueryable<T> queryable) => queryable.Skip(Skip).Take(Size);

    public static Flipper Create(int? index, int? size, int defaultSize, int maxSize) =>
        new(Math.Max(index ?? 1, 1), Math.Clamp(size ?? defaultSize, 1, maxSize));
}
