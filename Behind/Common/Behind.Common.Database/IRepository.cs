using System.Linq.Expressions;
using LaYumba.Functional;

namespace Behind.Common.Database;

public interface IRepository<TAggregate, TKey>
{
    TKey GetKey(TAggregate aggregate);
    
    Task<Option<TAggregate>> TryFindAsync(TKey key, CancellationToken cancellationToken = default);
    
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    
    void Update(TAggregate aggregate);
    
    void Delete(TAggregate aggregate);

    public async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        var aggregate = await TryFindAsync(key, cancellationToken);
        aggregate.Match(() => { }, Delete);
    }
}

public interface IRepository<T> : IRepository<T, Guid> where T : class;