using System.Text;

namespace Behind.Common;

public sealed record OrderByCollection(params IEnumerable<OrderBy> Collection);

public static class OrderByCollectionExtensions
{
    extension(string orderBy)
    {
        public OrderByCollection Create()
        {
            if (string.IsNullOrWhiteSpace(orderBy))
            {
                return new OrderByCollection();
            }
            
            orderBy = orderBy.Trim();
            
            var fields = orderBy.Split(',');

            List<OrderBy> list = new();
            
            foreach (var field in fields)
            {
                var parts = field.Split(' ');

                if (parts[0].Length < 2)
                {
                    continue;
                }
                
                if (parts.Length == 2)
                {
                    list.Add(new OrderBy(parts[0], parts[1] switch
                    {
                        "DESC" => Direction.Descending,
                        _ => Direction.Ascending
                    }));
                }
                else
                {
                    list.Add(new OrderBy(parts[0]));
                }
            }
            
            return new OrderByCollection(list);
        }
    }

    extension(OrderByCollection collection)
    {
        public string ToString()
        {
            var sb = new StringBuilder();
            var onlyOnce = new OnlyOnce();
            
            foreach (var orderBy in collection.Collection)
            {
                if (!onlyOnce.Apply())
                {
                    sb.Append(',');
                }
                
                sb.AppendFormat("{0} {1}",  orderBy.FieldName, orderBy.Direction switch
                {
                    Direction.Descending => "DESC",
                    _ => "ASC"
                });
            }
            
            return sb.ToString();
        }
    }
}