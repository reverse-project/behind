namespace Behind.Tests.Common;

public class FunctionalExtensionsTests
{
    // ── Curry ──────────────────────────────────────────────────────────────

    [Fact]
    public void Curry_TwoArgFunc_ReturnsCorrectResult()
    {
        Func<int, int, int> add = (a, b) => a + b;
        Func<int, Func<int, int>> c = add.Curry();
        Assert.Equal(5, c(2)(3));
    }

    [Fact]
    public void Curry_TwoArgFunc_IsPartiallyApplicable()
    {
        Func<int, int, int> multiply = (a, b) => a * b;
        var times3 = multiply.Curry()(3);
        Assert.Equal(12, times3(4));
        Assert.Equal(15, times3(5));
    }

    // ── CurryFirst ────────────────────────────────────────────────────────

    [Fact]
    public void CurryFirst_ThreeArgFunc_CurriesFirstArgOnly()
    {
        Func<int, int, int, int> add3 = (a, b, c) => a + b + c;
        Func<int, Func<int, int, int>> c = add3.CurryFirst();
        Assert.Equal(6, c(1)(2, 3));
    }

    [Fact]
    public void CurryFirst_ThreeArgFunc_RemainingArgsWorkCorrectly()
    {
        Func<int, int, int, int> sub = (a, b, c) => a - b - c;
        var from10 = sub.CurryFirst()(10);
        Assert.Equal(3, from10(5, 2));  // 10-5-2 = 3
    }

    // ── Tap ────────────────────────────────────────────────────────────────

    [Fact]
    public void Tap_ReturnsOriginalValue()
    {
        var tap = Tap<int>(x => { });
        var result = tap(42);
        Assert.Equal(42, result);
    }

    [Fact]
    public void Tap_RunsAction()
    {
        int captured = 0;
        var tap = Tap<int>(x => captured = x);
        tap(99);
        Assert.Equal(99, captured);
    }

    // ── Pipe(Func) ────────────────────────────────────────────────────────

    [Fact]
    public void Pipe_WithFunc_AppliesFunction()
    {
        var result = 5.Pipe(x => x * 2);
        Assert.Equal(10, result);
    }

    [Fact]
    public void Pipe_WithFunc_ChainsTransformations()
    {
        var result = 3.Pipe(x => x + 1).Pipe(x => x * 10);
        Assert.Equal(40, result);
    }

    // ── Pipe(Action) ──────────────────────────────────────────────────────

    [Fact]
    public void Pipe_WithAction_ReturnsOriginalValue()
    {
        var result = 7.Pipe(x => { _ = x; });
        Assert.Equal(7, result);
    }

    [Fact]
    public void Pipe_WithAction_RunsAction()
    {
        int captured = 0;
        5.Pipe(x => captured = x);
        Assert.Equal(5, captured);
    }

    // ── List ───────────────────────────────────────────────────────────────

    [Fact]
    public void List_WithItems_CreatesEnumerable()
    {
        var list = List(1, 2, 3);
        Assert.Equal(3, list.Count());
    }

    [Fact]
    public void List_WithItems_ContainsAllItems()
    {
        var list = List("a", "b", "c");
        Assert.Contains("a", list);
        Assert.Contains("b", list);
        Assert.Contains("c", list);
    }

    [Fact]
    public void List_Empty_ReturnsEmptyEnumerable()
    {
        var list = List<int>();
        Assert.Empty(list);
    }

    // ── Pair ───────────────────────────────────────────────────────────────

    [Fact]
    public void Pair_StoresKey()
    {
        var pair = Pair("myKey", 42);
        Assert.Equal("myKey", pair.Key);
    }

    [Fact]
    public void Pair_StoresValue()
    {
        var pair = Pair("myKey", 42);
        Assert.Equal(42, pair.Value);
    }

    // ── Unit ───────────────────────────────────────────────────────────────

    [Fact]
    public void Unit_ReturnsDefaultValueTuple()
    {
        var unit = Unit();
        Assert.Equal(default(ValueTuple), unit);
    }
}

// ── CommonExtensions.GetTimestamp ─────────────────────────────────────────────

public class CommonExtensionsTests
{
    [Fact]
    public void GetTimestamp_UuidV7_ReturnsEmbeddedTimestamp()
    {
        var ms = 1_700_000_000_000L; // a fixed Unix epoch ms
        var expected = DateTimeOffset.FromUnixTimeMilliseconds(ms);
        var guid = Guid.CreateVersion7(expected);

        var result = guid.GetTimestamp();

        Assert.InRange(result.ToUnixTimeMilliseconds(), ms - 1, ms + 1);
    }

    [Fact]
    public void GetTimestamp_DifferentTimestamp_ReturnsCorrectMs()
    {
        var ms = 1_000_000_000_000L;
        var expected = DateTimeOffset.FromUnixTimeMilliseconds(ms);
        var guid = Guid.CreateVersion7(expected);

        var result = guid.GetTimestamp();

        Assert.InRange(result.ToUnixTimeMilliseconds(), ms - 1, ms + 1);
    }

    [Fact]
    public void GetTimestamp_TwoGuidsCreatedAtDifferentTimes_ReturnsDifferentTimestamps()
    {
        var t1 = DateTimeOffset.FromUnixTimeMilliseconds(1_000_000_000_000L);
        var t2 = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000L);

        var g1 = Guid.CreateVersion7(t1);
        var g2 = Guid.CreateVersion7(t2);

        Assert.NotEqual(g1.GetTimestamp(), g2.GetTimestamp());
    }
}
