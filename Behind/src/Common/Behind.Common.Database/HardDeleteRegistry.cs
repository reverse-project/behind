using System.Collections.Concurrent;

namespace Behind.Common.Database;

public sealed class HardDeleteRegistry
{
    private readonly ConcurrentDictionary<object, byte> _entities = new(ReferenceEqualityComparer.Instance);

    public void Mark(object entity) => _entities.TryAdd(entity, 0);

    public bool IsMarked(object entity) => _entities.ContainsKey(entity);

    public void Clear() => _entities.Clear();
}
