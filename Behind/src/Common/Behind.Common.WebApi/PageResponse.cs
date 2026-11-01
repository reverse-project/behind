using Behind.Common;

namespace Behind.Common.WebApi;

public sealed record PageResponse<T>(long Total, long Page, long PageCount, int PageSize, IReadOnlyList<T> Items);

public static class PageResponseExtensions
{
    extension<T>(PagedResult<T> result)
    {
        public PageResponse<TItem> ToPageResponse<TItem>(Func<T, TItem> map) =>
            new(
                result.Total,
                result.Page,
                result.PageCount,
                result.PageSize,
                result.Results.Match(
                    () => (IReadOnlyList<TItem>)[],
                    items => items.Select(map).ToList()));
    }
}
