namespace Behind.Tests.Helpers;

public static class Wait
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(1);

    public static async Task<T> UntilAsync<T>(Func<T> read, Func<T, bool> condition, Action? retrigger = null)
    {
        using var timeout = new CancellationTokenSource(DefaultTimeout);
        var lastTrigger = TimeProvider.System.GetTimestamp();

        while (!condition(read()))
        {
            await Task.Delay(25, timeout.Token);

            if (retrigger is not null && TimeProvider.System.GetElapsedTime(lastTrigger) >= RetryEvery)
            {
                retrigger();
                lastTrigger = TimeProvider.System.GetTimestamp();
            }
        }

        return read();
    }
}
