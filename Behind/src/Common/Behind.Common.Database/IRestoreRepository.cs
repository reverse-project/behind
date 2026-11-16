namespace Behind.Common.Database;

public interface IRestoreRepository<in TAggregate, in TKey>
{
    Task RestoreAsync(TKey key, CancellationToken cancellationToken = default);
}
