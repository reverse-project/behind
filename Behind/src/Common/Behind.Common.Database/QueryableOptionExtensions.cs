using System.Linq.Expressions;

namespace Behind.Common.Database;

public static class QueryableOptionExtensions
{
    extension<TEntity>(IQueryable<TEntity> source)
    {
        public IQueryable<TEntity> Narrowed<TValue>(
            Option<TValue> value,
            Func<TValue, Expression<Func<TEntity, bool>>> predicate) =>
            value.Match(() => source, found => source.Where(predicate(found)));
    }
}
