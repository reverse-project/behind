using System.Linq.Expressions;

namespace Behind.Common;

public record Filter<T>(Expression<Func<T, bool>> Predicate)
{
    public IQueryable<T> Apply(IQueryable<T> queryable) => queryable.Where(Predicate);
}

public sealed record EmptyFilter<T>() : Filter<T>(t => true);