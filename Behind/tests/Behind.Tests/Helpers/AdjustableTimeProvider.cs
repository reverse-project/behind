namespace Behind.Tests.Helpers;

public sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private long _ticks = now.UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    public void Advance(TimeSpan span) => Interlocked.Add(ref _ticks, span.Ticks);
}
