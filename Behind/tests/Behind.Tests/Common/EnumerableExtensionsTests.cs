namespace Behind.Tests.Common;

public class EnumerableExtensionsTests
{
    // ── Head ───────────────────────────────────────────────────────────────

    [Fact]
    public void Head_NonEmptyList_ReturnsSomeOfFirst()
    {
        var list = new[] { 10, 20, 30 };
        var result = list.Head();
        Assert.Equal(Some(10), result);
    }

    [Fact]
    public void Head_EmptyList_ReturnsNone()
    {
        var list = Array.Empty<int>();
        var result = list.Head();
        Option<int> none = None;
        Assert.Equal(none, result);
    }

    [Fact]
    public void Head_NullList_ReturnsNone()
    {
        IEnumerable<int>? list = null;
        var result = list.Head();
        Option<int> none = None;
        Assert.Equal(none, result);
    }

    // ── Find ───────────────────────────────────────────────────────────────

    [Fact]
    public void Find_ElementExists_ReturnsSome()
    {
        var list = new[] { 1, 2, 3, 4 };
        var result = list.Find(x => x > 2);
        Assert.Equal(Some(3), result);
    }

    [Fact]
    public void Find_NoMatch_ReturnsNone()
    {
        var list = new[] { 1, 2, 3 };
        var result = list.Find(x => x > 100);
        Option<int> none = None;
        Assert.Equal(none, result);
    }

    [Fact]
    public void Find_EmptyList_ReturnsNone()
    {
        var result = Array.Empty<int>().Find(x => x == 1);
        Option<int> none = None;
        Assert.Equal(none, result);
    }

    // ── FirstOr ────────────────────────────────────────────────────────────

    [Fact]
    public void FirstOr_NonEmpty_ReturnsFirst()
    {
        var list = new[] { 7, 8, 9 };
        Assert.Equal(7, list.FirstOr(-1));
    }

    [Fact]
    public void FirstOr_Empty_ReturnsDefault()
    {
        var list = Array.Empty<int>();
        Assert.Equal(-1, list.FirstOr(-1));
    }

    // ── Map ────────────────────────────────────────────────────────────────

    [Fact]
    public void Map_TransformsAllElements()
    {
        var list = new[] { 1, 2, 3 };
        var result = list.Map(x => x * 10).ToList();
        Assert.Equal(new[] { 10, 20, 30 }, result);
    }

    [Fact]
    public void Map_EmptyList_ReturnsEmpty()
    {
        var result = Array.Empty<int>().Map(x => x * 10);
        Assert.Empty(result);
    }

    // ── Bind ───────────────────────────────────────────────────────────────

    [Fact]
    public void Bind_FlatMapsElements()
    {
        var list = new[] { 1, 2, 3 };
        var result = list.Bind(x => new[] { x, x * 10 }).ToList();
        Assert.Equal(new[] { 1, 10, 2, 20, 3, 30 }, result);
    }

    [Fact]
    public void Bind_EmptyList_ReturnsEmpty()
    {
        var result = Array.Empty<int>().Bind(x => new[] { x });
        Assert.Empty(result);
    }

    // ── Append ─────────────────────────────────────────────────────────────

    [Fact]
    public void Append_AddsElementsToEnd()
    {
        var list = new[] { 1, 2, 3 };
        var result = list.Append(4, 5).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result);
    }

    [Fact]
    public void Append_EmptySource_ReturnsAppendedElements()
    {
        var result = Array.Empty<int>().Append(1, 2).ToList();
        Assert.Equal(new[] { 1, 2 }, result);
    }

    // ── Match ──────────────────────────────────────────────────────────────

    [Fact]
    public void Match_EmptyList_CallsEmptyFunc()
    {
        bool calledEmpty = false;
        Array.Empty<int>().Match(
            empty: () => { calledEmpty = true; return 0; },
            otherwise: (head, tail) => 1);
        Assert.True(calledEmpty);
    }

    [Fact]
    public void Match_NonEmptyList_CallsOtherwiseWithHeadAndTail()
    {
        var list = new[] { 10, 20, 30 };
        var (head, tailCount) = list.Match(
            empty: () => (0, 0),
            otherwise: (h, t) => (h, t.Count()));
        Assert.Equal(10, head);
        Assert.Equal(2, tailCount);
    }

    [Fact]
    public void Match_SingleElement_TailIsEmpty()
    {
        var list = new[] { 42 };
        var tailCount = list.Match(
            empty: () => -1,
            otherwise: (h, t) => t.Count());
        Assert.Equal(0, tailCount);
    }

    // ── ForEach ────────────────────────────────────────────────────────────

    [Fact]
    public void ForEach_RunsActionForEachElement()
    {
        var captured = new List<int>();
        var list = new[] { 1, 2, 3 };
        list.ForEach(x => captured.Add(x)).ToList(); // materialise to trigger execution
        Assert.Equal(new[] { 1, 2, 3 }, captured);
    }

    [Fact]
    public void ForEach_EmptyList_RunsActionZeroTimes()
    {
        int count = 0;
        Array.Empty<int>().ForEach(_ => count++).ToList();
        Assert.Equal(0, count);
    }

    [Fact]
    public void ForEach_ReturnsEnumerableOfUnit()
    {
        var result = new[] { 1, 2, 3 }.ForEach(_ => { }).ToList();
        Assert.Equal(3, result.Count);
    }
}
