using Behind.Common.Logging;

namespace Behind.Tests.Common;

public class CorrelationContextTests
{
    [Fact]
    public void Current_WithoutScope_ReturnsStableProcessId()
    {
        Assert.Equal(CorrelationContext.ProcessId, CorrelationContext.Current);
        Assert.True(Guid.TryParse(CorrelationContext.ProcessId, out var guid));
        Assert.Equal(7, guid.Version);
    }

    [Fact]
    public void Begin_SetsCurrentAndRestoresPrevious()
    {
        using (CorrelationContext.Begin("outer"))
        {
            Assert.Equal("outer", CorrelationContext.Current);

            using (CorrelationContext.Begin("inner"))
            {
                Assert.Equal("inner", CorrelationContext.Current);
            }

            Assert.Equal("outer", CorrelationContext.Current);
        }

        Assert.Equal(CorrelationContext.ProcessId, CorrelationContext.Current);
    }

    [Fact]
    public async Task Current_FlowsAcrossAwaitsAndTasks()
    {
        using var _ = CorrelationContext.Begin("flow");

        var seen = await Task.WhenAll(
            Task.Run(() => CorrelationContext.Current),
            Task.Run(async () =>
            {
                await Task.Yield();
                return CorrelationContext.Current;
            }));

        Assert.All(seen, id => Assert.Equal("flow", id));
    }

    [Fact]
    public async Task Begin_InsideAsyncMethod_DoesNotLeakToCaller()
    {
        await Task.Run(async () =>
        {
            using var _ = CorrelationContext.Begin("scoped");
            await Task.Yield();
        });

        Assert.Equal(CorrelationContext.ProcessId, CorrelationContext.Current);
    }
}
