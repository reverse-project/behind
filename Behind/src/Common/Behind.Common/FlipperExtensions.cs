using static Behind.Common.FunctionalExtensions;

namespace Behind.Common;

public static class FlipperExtensions
{
    extension(Flipper flipper)
    {
        public PagedResult<T> ToPage<T>(long total, IReadOnlyList<T> rows) =>
            new(
                total,
                flipper.Index,
                flipper.PageCount(total),
                flipper.Size,
                rows.Count > 0 ? Some<IEnumerable<T>>(rows) : None);
    }
}
