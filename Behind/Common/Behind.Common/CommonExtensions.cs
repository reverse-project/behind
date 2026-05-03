using System.Linq.Expressions;

namespace Behind.Common;

public static class CommonExtensions
{
    extension(OrderByCollection collection)
    {
        public IEnumerable<T> Apply<T>(IEnumerable<T> list) where T : class => collection.Apply(list.AsQueryable());

        public IQueryable<T> Apply<T>(IQueryable<T> query) where T : class
        {
            if (!collection.Collection.Any())
            {
                return query;
            }

            var expression = query.Expression;
            var parameter = Expression.Parameter(typeof(T), "x");
            bool first = true;

            foreach (var orderBy in collection.Collection)
            {
                Expression property = orderBy.FieldName
                    .Split('.')
                    .Aggregate<string?, Expression>(parameter, Expression.PropertyOrField!);

                var lambda = Expression.Lambda(property, parameter);

                string methodName = first
                    ? (orderBy.Direction == Direction.Descending ? "OrderByDescending" : "OrderBy")
                    : (orderBy.Direction == Direction.Descending ? "ThenByDescending" : "ThenBy");

                expression = Expression.Call(
                    typeof(Queryable),
                    methodName,
                    new[] { typeof(T), property.Type },
                    expression,
                    Expression.Quote(lambda));

                first = false;
            }

            return query.Provider.CreateQuery<T>(expression);
        }
    }
}