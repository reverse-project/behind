namespace Behind.Common.Database;

public interface IHardDeleteRepository<in TAggregate, in TKey>
{
    void HardDelete(TAggregate aggregate);

    Task HardDeleteAsync(TKey key, CancellationToken cancellationToken = default);
}
