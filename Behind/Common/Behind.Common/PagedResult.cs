using LaYumba.Functional;

namespace Behind.Common;

public sealed record PagedResult<TAggregate>(
    long Total,
    long Page,
    long PageCount,
    int PageSize,
    Option<IEnumerable<TAggregate>> Results
);