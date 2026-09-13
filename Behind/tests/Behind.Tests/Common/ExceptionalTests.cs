namespace Behind.Tests.Common;

public class ExceptionalTests
{
    // ── Construction / state ───────────────────────────────────────────────

    [Fact]
    public void ImplicitFromValue_SuccessIsTrue()
    {
        Exceptional<int> ex = 42;
        Assert.True(ex.Success);
    }

    [Fact]
    public void ImplicitFromValue_ExceptionIsFalse()
    {
        Exceptional<int> ex = 42;
        Assert.False(ex.Exception);
    }

    [Fact]
    public void ImplicitFromException_SuccessIsFalse()
    {
        Exceptional<int> ex = new InvalidOperationException("boom");
        Assert.False(ex.Success);
    }

    [Fact]
    public void ImplicitFromException_ExceptionIsTrue()
    {
        Exceptional<int> ex = new InvalidOperationException("boom");
        Assert.True(ex.Exception);
    }

    // ── Match ──────────────────────────────────────────────────────────────

    // For Exceptional<int>, the Match success branch receives T? which for an
    // unconstrained value type T resolves to plain T (int), not Nullable<int>.

    [Fact]
    public void Match_Success_CallsSuccessBranch()
    {
        Exceptional<int> ex = 10;
        var result = ex.Match(e => -1, v => v * 2);
        Assert.Equal(20, result);
    }

    [Fact]
    public void Match_Exception_CallsExceptionBranch()
    {
        Exceptional<int> ex = new Exception("fail");
        var result = ex.Match(e => e.Message, v => "ok");
        Assert.Equal("fail", result);
    }

    // ── Map ────────────────────────────────────────────────────────────────

    [Fact]
    public void Map_Success_TransformsValue()
    {
        Exceptional<int> ex = 5;
        var result = ex.Map(v => v * 3);
        Assert.True(result.Success);
        var value = result.Match(e => -1, v => v);
        Assert.Equal(15, value);
    }

    [Fact]
    public void Map_Exception_PassesThroughException()
    {
        var original = new Exception("original");
        Exceptional<int> ex = original;
        var result = ex.Map(v => v * 3);
        Assert.True(result.Exception);
        var msg = result.Match(e => e.Message, _ => "none");
        Assert.Equal("original", msg);
    }

    // ── Bind ───────────────────────────────────────────────────────────────

    [Fact]
    public void Bind_Success_ChainsFunction()
    {
        Exceptional<int> ex = 4;
        // Bind receives T? which for int resolves to int in unconstrained generics
        var result = ex.Bind(v => (Exceptional<string>)v.ToString());
        Assert.True(result.Success);
        var value = result.Match(e => "err", v => v ?? "");
        Assert.Equal("4", value);
    }

    [Fact]
    public void Bind_Exception_DoesNotCallFunction()
    {
        bool called = false;
        Exceptional<int> ex = new Exception("stop");
        var result = ex.Bind(v =>
        {
            called = true;
            return (Exceptional<string>)"ok";
        });
        Assert.False(called);
        Assert.True(result.Exception);
    }

    [Fact]
    public void Bind_Success_FunctionReturnsException_PropagatesException()
    {
        Exceptional<int> ex = 5;
        var inner = new Exception("inner");
        var result = ex.Bind<int, string>(_ => inner);
        Assert.True(result.Exception);
    }

    // ── LINQ Select ────────────────────────────────────────────────────────

    [Fact]
    public void Select_Success_TransformsValue()
    {
        var result = from x in (Exceptional<int>)5 select x * 2;
        Assert.True(result.Success);
        Assert.Equal(10, result.Match(e => -1, v => v));
    }

    [Fact]
    public void Select_Exception_PassesThrough()
    {
        var result = from x in (Exceptional<int>)new Exception("err") select x * 2;
        Assert.True(result.Exception);
    }

    // ── LINQ SelectMany ────────────────────────────────────────────────────

    [Fact]
    public void SelectMany_BothSuccess_ReturnsProjected()
    {
        var result =
            from x in (Exceptional<int>)3
            from y in (Exceptional<int>)4
            select x + y;
        Assert.True(result.Success);
        Assert.Equal(7, result.Match(e => -1, v => v));
    }

    [Fact]
    public void SelectMany_FirstException_StopsChain()
    {
        var result =
            from x in (Exceptional<int>)new Exception("first")
            from y in (Exceptional<int>)4
            select x + y;
        Assert.True(result.Exception);
    }

    [Fact]
    public void SelectMany_SecondException_StopsChain()
    {
        var result =
            from x in (Exceptional<int>)3
            from y in (Exceptional<int>)new Exception("second")
            select x + y;
        Assert.True(result.Exception);
    }

    // ── ToString ───────────────────────────────────────────────────────────

    [Fact]
    public void ToString_Success_ContainsSuccessAndValue()
    {
        Exceptional<int> ex = 42;
        var str = ex.ToString();
        Assert.Contains("Success", str);
        Assert.Contains("42", str);
    }

    [Fact]
    public void ToString_Exception_ContainsExceptionAndMessage()
    {
        Exceptional<int> ex = new Exception("boom");
        var str = ex.ToString();
        Assert.Contains("Exception", str);
        Assert.Contains("boom", str);
    }
}
