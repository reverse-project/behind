namespace Behind.Tests.Common;

public class ValidationTests
{
    // ── Construction / IsValid ─────────────────────────────────────────────

    [Fact]
    public void Valid_IsValidTrue()
    {
        var v = Valid(42);
        Assert.True(v.IsValid);
    }

    [Fact]
    public void Invalid_IsValidFalse()
    {
        var v = Invalid<int>(Error("bad"));
        Assert.False(v.IsValid);
    }

    [Fact]
    public void ImplicitFromT_IsValid()
    {
        Validation<int> v = 42;
        Assert.True(v.IsValid);
    }

    [Fact]
    public void ImplicitFromError_IsInvalid()
    {
        Validation<int> v = Error("problem");
        Assert.False(v.IsValid);
    }

    [Fact]
    public void ImplicitFromInvalid_IsInvalid()
    {
        Validation<int> v = Invalid(Error("x"), Error("y"));
        Assert.False(v.IsValid);
    }

    // ── Match ──────────────────────────────────────────────────────────────

    [Fact]
    public void Match_Valid_ReturnsValidBranchResult()
    {
        var result = Valid(10).Match(errs => -1, v => v * 2);
        Assert.Equal(20, result);
    }

    [Fact]
    public void Match_Invalid_ReturnsInvalidBranchResult()
    {
        var result = Invalid<int>(Error("oops")).Match(errs => -1, v => v * 2);
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Match_Invalid_PassesErrorsToInvalidBranch()
    {
        var errorMsg = "bad input";
        var errors = Invalid<int>(Error(errorMsg))
            .Match(errs => errs.Select(e => e.Message ?? "").ToList(), _ => new List<string>());
        Assert.Contains(errorMsg, errors);
    }

    // ── Map ────────────────────────────────────────────────────────────────

    [Fact]
    public void Map_Valid_TransformsValue()
    {
        var result = Valid(5).Map(x => x * 3);
        Assert.True(result.IsValid);
        var value = result.Match(_ => -1, v => v);
        Assert.Equal(15, value);
    }

    [Fact]
    public void Map_Invalid_PassesThroughErrors()
    {
        var result = Invalid<int>(Error("err")).Map(x => x * 3);
        Assert.False(result.IsValid);
    }

    // ── Apply ──────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_ValidFuncAndValidValue_ReturnsValid()
    {
        Validation<Func<int, int>> f = Valid<Func<int, int>>(x => x * 2);
        var result = f.Apply(Valid(5));
        var value = result.Match(_ => -1, v => v);
        Assert.Equal(10, value);
    }

    [Fact]
    public void Apply_ValidFuncAndInvalidValue_ReturnsInvalid()
    {
        Validation<Func<int, int>> f = Valid<Func<int, int>>(x => x * 2);
        var result = f.Apply(Invalid<int>(Error("bad value")));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Apply_InvalidFuncAndValidValue_ReturnsInvalid()
    {
        Validation<Func<int, int>> f = Invalid<Func<int, int>>(Error("bad func"));
        var result = f.Apply(Valid(5));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Apply_BothInvalid_AccumulatesErrors()
    {
        Validation<Func<int, int>> f = Invalid<Func<int, int>>(Error("bad func"));
        Validation<int> v = Invalid<int>(Error("bad value"));
        var result = f.Apply(v);
        Assert.False(result.IsValid);
        var errorCount = result.Match(errs => errs.Count(), _ => 0);
        Assert.Equal(2, errorCount);
    }

    // ── Bind ───────────────────────────────────────────────────────────────

    [Fact]
    public void Bind_Valid_AppliesFunction()
    {
        var result = Valid(5).Bind(x => x > 3 ? Valid(x * 2) : Invalid<int>(Error("too small")));
        var value = result.Match(_ => -1, v => v);
        Assert.Equal(10, value);
    }

    [Fact]
    public void Bind_Valid_FunctionReturnsInvalid_ReturnsInvalid()
    {
        var result = Valid(1).Bind(x => x > 10 ? Valid(x) : Invalid<int>(Error("too small")));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Bind_Invalid_DoesNotCallFunction()
    {
        bool called = false;
        var result = Invalid<int>(Error("err")).Bind(x =>
        {
            called = true;
            return Valid(x);
        });
        Assert.False(called);
        Assert.False(result.IsValid);
    }

    // ── LINQ Select ────────────────────────────────────────────────────────

    [Fact]
    public void Select_Valid_TransformsValue()
    {
        var result = from x in Valid(4) select x * 3;
        var value = result.Match(_ => -1, v => v);
        Assert.Equal(12, value);
    }

    [Fact]
    public void Select_Invalid_ReturnsInvalid()
    {
        var result = from x in Invalid<int>(Error("err")) select x * 3;
        Assert.False(result.IsValid);
    }

    // ── LINQ SelectMany ────────────────────────────────────────────────────

    [Fact]
    public void SelectMany_BothValid_ReturnsProjected()
    {
        var result = from x in Valid(3)
                     from y in Valid(4)
                     select x + y;
        var value = result.Match(_ => -1, v => v);
        Assert.Equal(7, value);
    }

    [Fact]
    public void SelectMany_FirstInvalid_ReturnsInvalid()
    {
        var result = from x in Invalid<int>(Error("e1"))
                     from y in Valid(4)
                     select x + y;
        Assert.False(result.IsValid);
    }

    [Fact]
    public void SelectMany_SecondInvalid_ReturnsInvalid()
    {
        var result = from x in Valid(3)
                     from y in Invalid<int>(Error("e2"))
                     select x + y;
        Assert.False(result.IsValid);
    }

    // ── ToString ───────────────────────────────────────────────────────────

    [Fact]
    public void ToString_Valid_ContainsValue()
    {
        var str = Valid(42).ToString();
        Assert.Contains("42", str);
    }

    [Fact]
    public void ToString_Invalid_ContainsInvalid()
    {
        var str = Invalid<int>(Error("oops")).ToString();
        Assert.Contains("Invalid", str);
    }
}
