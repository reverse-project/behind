namespace Behind.Tests.Helpers;

public static class Wait
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public static async Task<T> UntilAsync<T>(Func<T> read, Func<T, bool> condition)
    {
        using var timeout = new CancellationTokenSource(DefaultTimeout);

        while (!condition(read()))
        {
            await Task.Delay(25, timeout.Token);
        }

        return read();
    }
}
