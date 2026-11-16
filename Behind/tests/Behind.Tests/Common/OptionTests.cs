namespace Behind.Tests.Common;

public class OptionTests
{
    // ── Construction / state ───────────────────────────────────────────────

    [Fact]
    public void Some_WrapsValue_IsSome()
    {
        var opt = Some(42);
        bool isSome = opt.Match(() => false, _ => true);
        Assert.True(isSome);
    }

    [Fact]
    public void None_IsNone()
    {
        Option<int> opt = None;
        bool isNone = opt.Match(() => true, _ => false);
        Assert.True(isNone);
    }

    [Fact]
    public void IsNone_ReportsWhetherTheOptionIsEmpty()
    {
        Option<int> none = None;

        Assert.True(none.IsNone());
        Assert.False(Some(1).IsNone());
    }

    [Fact]
    public void ImplicitFromValue_NonNull_IsSome()
    {
        Option<string> opt = "hello";
        bool isSome = opt.Match(() => false, _ => true);
        Assert.True(isSome);
    }

    [Fact]
    public void ImplicitFromNullReference_IsNone()
    {
        Option<string> opt = (string)(object)null!;
        bool isNone = opt.Match(() => true, _ => false);
        Assert.True(isNone);
    }

    // ── Match ──────────────────────────────────────────────────────────────

    [Fact]
    public void Match_Some_ReturnsSomeBranchResult()
    {
        var result = Some(10).Match(() => -1, v => v * 2);
        Assert.Equal(20, result);
    }

    [Fact]
    public void Match_None_ReturnsNoneBranchResult()
    {
        Option<int> opt = None;
        var result = opt.Match(() => -1, v => v * 2);
        Assert.Equal(-1, result);
    }

    // ── Equality ───────────────────────────────────────────────────────────

    [Fact]
    public void SomeEquality_SameValue_AreEqual()
    {
        Assert.Equal(Some(1), Some(1));
    }

    [Fact]
    public void SomeEquality_DifferentValues_AreNotEqual()
    {
        Assert.NotEqual(Some(1), Some(2));
    }

    [Fact]
    public void NoneEquality_TwoNones_AreEqual()
    {
        Option<int> a = None;
        Option<int> b = None;
        Assert.Equal(a, b);
    }

    [Fact]
    public void SomeVsNone_AreNotEqual()
    {
        Option<int> none = None;
        Assert.NotEqual(Some(1), none);
    }

    [Fact]
    public void EqualOperator_SameValue_ReturnsTrue()
    {
        Assert.True(Some("x") == Some("x"));
    }

    [Fact]
    public void NotEqualOperator_DifferentValues_ReturnsTrue()
    {
        Assert.True(Some("x") != Some("y"));
    }

    [Fact]
    public void NoneEqualsNone_ViaEqualsMethod()
    {
        Option<int> a = None;
        Assert.True(a.Equals(None));
    }

    // ── GetHashCode ────────────────────────────────────────────────────────

    [Fact]
    public void GetHashCode_SameValue_IsConsistent()
    {
        Assert.Equal(Some(5).GetHashCode(), Some(5).GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentSomeValues_AreGenerallyDifferent()
    {
        // Not guaranteed by contract but holds for simple ints
        Assert.NotEqual(Some(1).GetHashCode(), Some(999).GetHashCode());
    }

    // ── ToString ───────────────────────────────────────────────────────────

    [Fact]
    public void ToString_Some_ReturnsSomeFormat()
    {
        Assert.Equal("Some(5)", Some(5).ToString());
    }

    [Fact]
    public void ToString_None_ReturnsNone()
    {
        Option<int> opt = None;
        Assert.Equal("None", opt.ToString());
    }

    // ── AsEnumerable ───────────────────────────────────────────────────────

    [Fact]
    public void AsEnumerable_Some_YieldsOneElement()
    {
        Assert.Single(Some(42).AsEnumerable());
    }

    [Fact]
    public void AsEnumerable_Some_YieldsCorrectValue()
    {
        Assert.Equal(42, Some(42).AsEnumerable().Single());
    }

    [Fact]
    public void AsEnumerable_None_YieldsNoElements()
    {
        Option<int> opt = None;
        Assert.Empty(opt.AsEnumerable());
    }

    // ── Map ────────────────────────────────────────────────────────────────

    [Fact]
    public void Map_Some_TransformsValue()
    {
        var result = Some(5).Map(x => x * 3);
        Assert.Equal(Some(15), result);
    }

    [Fact]
    public void Map_None_ReturnsNone()
    {
        Option<int> opt = None;
        var result = opt.Map(x => x * 3);
        Option<int> none = None;
        Assert.Equal(none, result);
    }

    // ── Bind ───────────────────────────────────────────────────────────────

    [Fact]
    public void Bind_Some_ReturnsInnerOption()
    {
        var result = Some(5).Bind(x => x > 3 ? Some(x * 2) : (Option<int>)None);
        Assert.Equal(Some(10), result);
    }

    [Fact]
    public void Bind_Some_WhenFunctionReturnsNone_ReturnsNone()
    {
        var result = Some(1).Bind(x => x > 10 ? Some(x) : (Option<int>)None);
        Option<int> none = None;
        Assert.Equal(none, result);
    }

    [Fact]
    public void Bind_None_ReturnsNone()
    {
        Option<int> opt = None;
        var result = opt.Bind(x => Some(x * 2));
        Option<int> none = None;
        Assert.Equal(none, result);
    }

    // ── GetOrElse ──────────────────────────────────────────────────────────

    [Fact]
    public void GetOrElse_Some_ReturnsValue()
    {
        Assert.Equal(5, Some(5).GetOrElse(0));
    }

    [Fact]
    public void GetOrElse_None_ReturnsDefault()
    {
        Option<int> opt = None;
        Assert.Equal(99, opt.GetOrElse(99));
    }

    [Fact]
    public void GetOrElse_WithFallbackFunc_Some_ReturnsValue()
    {
        Assert.Equal(5, Some(5).GetOrElse(() => 0));
    }

    [Fact]
    public void GetOrElse_WithFallbackFunc_None_CallsFallback()
    {
        Option<int> opt = None;
        Assert.Equal(7, opt.GetOrElse(() => 7));
    }

    // ── OrElse ─────────────────────────────────────────────────────────────

    [Fact]
    public void OrElse_Some_ReturnsLeft()
    {
        var result = Some(5).OrElse(Some(10));
        Assert.Equal(Some(5), result);
    }

    [Fact]
    public void OrElse_None_ReturnsRight()
    {
        Option<int> opt = None;
        var result = opt.OrElse(Some(10));
        Assert.Equal(Some(10), result);
    }

    [Fact]
    public void OrElse_WithFunc_Some_ReturnsLeft()
    {
        var result = Some(5).OrElse(() => Some(10));
        Assert.Equal(Some(5), result);
    }

    [Fact]
    public void OrElse_WithFunc_None_CallsFunc()
    {
        Option<int> opt = None;
        var result = opt.OrElse(() => Some(10));
        Assert.Equal(Some(10), result);
    }

    // ── Where ──────────────────────────────────────────────────────────────

    [Fact]
    public void Where_Some_PredicatePasses_ReturnsSome()
    {
        var result = Some(5).Where(x => x > 3);
        Assert.Equal(Some(5), result);
    }

    [Fact]
    public void Where_Some_PredicateFails_ReturnsNone()
    {
        var result = Some(1).Where(x => x > 10);
        Option<int> none = None;
        Assert.Equal(none, result);
    }

    [Fact]
    public void Where_None_ReturnsNone()
    {
        Option<int> opt = None;
        var result = opt.Where(x => x > 3);
        Option<int> none = None;
        Assert.Equal(none, result);
    }

    // ── Select (LINQ) ──────────────────────────────────────────────────────

    [Fact]
    public void Select_Some_TransformsValue()
    {
        var result = Some(5).Select(x => x.ToString());
        Assert.Equal(Some("5"), result);
    }

    [Fact]
    public void Select_None_ReturnsNone()
    {
        Option<int> opt = None;
        var result = opt.Select(x => x.ToString());
        Option<string> none = None;
        Assert.Equal(none, result);
    }

    [Fact]
    public void LinqQuery_Some_TransformsValue()
    {
        var result = from x in Some(4) select x * 2;
        Assert.Equal(Some(8), result);
    }

    // ── SelectMany (LINQ) ─────────────────────────────────────────────────

    [Fact]
    public void SelectMany_BothSome_ReturnsProjected()
    {
        var result = from x in Some(3)
                     from y in Some(4)
                     select x + y;
        Assert.Equal(Some(7), result);
    }

    [Fact]
    public void SelectMany_FirstNone_ReturnsNone()
    {
        Option<int> none = None;
        var result = from x in none
                     from y in Some(4)
                     select x + y;
        Option<int> expected = None;
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SelectMany_SecondNone_ReturnsNone()
    {
        Option<int> none = None;
        var result = from x in Some(3)
                     from y in none
                     select x + y;
        Option<int> expected = None;
        Assert.Equal(expected, result);
    }

    // ── ForEach ────────────────────────────────────────────────────────────

    [Fact]
    public void ForEach_Some_RunsAction()
    {
        int captured = 0;
        Some(42).ForEach(x => captured = x);
        Assert.Equal(42, captured);
    }

    [Fact]
    public void ForEach_None_DoesNotRunAction()
    {
        int captured = 0;
        Option<int> opt = None;
        opt.ForEach(x => captured = x);
        Assert.Equal(0, captured);
    }
}
