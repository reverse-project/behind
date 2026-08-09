namespace Behind.Common;

public sealed class SensitiveContext : IDisposable
{
    private static readonly ThreadLocal<SensitiveContext?> Current = new();
    private bool _disposed;

    public bool IsActive => !_disposed && Current.Value == this;

    public SensitiveContext()
    {
        Current.Value = this;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (Current.Value == this)
            Current.Value = null;
    }

    public static SensitiveContext? GetCurrent() => Current.Value;

    public static void VerifyContextActive()
    {
        if (Current.Value is null || !Current.Value.IsActive)
            throw new InvalidOperationException(
                "SensitiveContext is not active. Use: using (var ctx = new SensitiveContext()) { ... }");
    }
}
