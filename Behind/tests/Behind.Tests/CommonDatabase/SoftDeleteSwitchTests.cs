namespace Behind.Tests.CommonDatabase;

public sealed class SoftDeleteSwitchTests
{
    [Fact]
    public void IncludesDeleted_ByDefault_IsFalse() =>
        Assert.False(new SoftDeleteSwitch().IncludesDeleted);

    [Fact]
    public void IncludeDeleted_WhileScopeIsOpen_IsTrue()
    {
        var softDelete = new SoftDeleteSwitch();

        using (softDelete.IncludeDeleted())
        {
            Assert.True(softDelete.IncludesDeleted);
        }

        Assert.False(softDelete.IncludesDeleted);
    }

    [Fact]
    public void IncludeDeleted_NestedScopes_StayOnUntilOutermostIsDisposed()
    {
        var softDelete = new SoftDeleteSwitch();
        var outer = softDelete.IncludeDeleted();
        var inner = softDelete.IncludeDeleted();

        inner.Dispose();
        Assert.True(softDelete.IncludesDeleted);

        outer.Dispose();
        Assert.False(softDelete.IncludesDeleted);
    }

    [Fact]
    public void Dispose_CalledTwice_ReleasesTheScopeOnlyOnce()
    {
        var softDelete = new SoftDeleteSwitch();
        var first = softDelete.IncludeDeleted();
        using var second = softDelete.IncludeDeleted();

        first.Dispose();
        first.Dispose();

        Assert.True(softDelete.IncludesDeleted);
    }

    [Fact]
    public async Task IncludeDeleted_ConcurrentScopes_BalanceOut()
    {
        var softDelete = new SoftDeleteSwitch();

        await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => Task.Run(() =>
        {
            using var scope = softDelete.IncludeDeleted();
            Assert.True(softDelete.IncludesDeleted);
        })));

        Assert.False(softDelete.IncludesDeleted);
    }
}
