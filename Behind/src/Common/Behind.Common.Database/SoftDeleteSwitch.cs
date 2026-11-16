namespace Behind.Common.Database;

public sealed class SoftDeleteSwitch
{
    private int _depth;

    public bool IncludesDeleted => Volatile.Read(ref _depth) > 0;

    public IDisposable IncludeDeleted()
    {
        Interlocked.Increment(ref _depth);
        return new Scope(this);
    }

    private sealed class Scope(SoftDeleteSwitch owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Interlocked.Decrement(ref owner._depth);
            }
        }
    }
}
