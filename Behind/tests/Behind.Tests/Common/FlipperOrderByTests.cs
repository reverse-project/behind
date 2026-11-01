namespace Behind.Tests.Common;

// ── Flipper ───────────────────────────────────────────────────────────────────

public class FlipperTests
{
    // Skip

    [Theory]
    [InlineData(1, 3, 0)]   // first page → no skip
    [InlineData(2, 3, 3)]   // second page → skip 1*Size
    [InlineData(3, 3, 6)]   // third page  → skip 2*Size
    public void Skip_ReturnsCorrectValue(int index, int size, int expectedSkip)
    {
        var flipper = new Flipper(index, size);
        Assert.Equal(expectedSkip, flipper.Skip);
    }

    [Fact]
    public void Skip_IndexOne_IsZero()
    {
        var flipper = new Flipper(1, 10);
        Assert.Equal(0, flipper.Skip);
    }

    [Fact]
    public void Skip_IndexGreaterThanOne_IsIndexMinusOneTimesSize()
    {
        var flipper = new Flipper(4, 5);
        Assert.Equal(15, flipper.Skip); // (4-1)*5 = 15
    }

    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(1, 3, 1)]
    [InlineData(3, 3, 1)]
    [InlineData(4, 3, 2)]
    [InlineData(6, 3, 2)]
    [InlineData(7, 3, 3)]
    [InlineData(95, 10, 10)]
    [InlineData(100, 10, 10)]
    [InlineData(101, 10, 11)]
    public void PageCount_ReturnsTheNumberOfPagesNeeded(long total, int size, long expected)
    {
        Assert.Equal(expected, new Flipper(1, size).PageCount(total));
    }

    [Fact]
    public void PageCount_NegativeTotalOrNonPositiveSize_ReturnsZero()
    {
        Assert.Equal(0L, new Flipper(1, 3).PageCount(-5));
        Assert.Equal(0L, new Flipper(1, 0).PageCount(5));
    }

    [Theory]
    [InlineData(null, null, 1, 10)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-3, -1, 1, 1)]
    [InlineData(4, 25, 4, 25)]
    [InlineData(2, 5000, 2, 100)]
    public void Create_AppliesDefaultsAndClampsToTheAllowedRange(int? index, int? size, int expectedIndex, int expectedSize)
    {
        var flipper = Flipper.Create(index, size, 10, 100);

        Assert.Equal(new Flipper(expectedIndex, expectedSize), flipper);
    }

    // Apply

    [Fact]
    public void Apply_FirstPage_TakesFirstNElements()
    {
        var flipper = new Flipper(1, 3);
        var result = flipper.Apply(Enumerable.Range(1, 10).AsQueryable()).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void Apply_SecondPage_SkipsAndTakesCorrectElements()
    {
        var flipper = new Flipper(2, 3);
        var result = flipper.Apply(Enumerable.Range(1, 10).AsQueryable()).ToList();
        Assert.Equal(new[] { 4, 5, 6 }, result);
    }

    [Fact]
    public void Apply_ThirdPage_SkipsAndTakesCorrectElements()
    {
        var flipper = new Flipper(3, 3);
        var result = flipper.Apply(Enumerable.Range(1, 10).AsQueryable()).ToList();
        Assert.Equal(new[] { 7, 8, 9 }, result);
    }
}

// ── OrderByCollection.Create ──────────────────────────────────────────────────

public class OrderByCollectionCreateTests
{
    [Fact]
    public void Create_EmptyString_ReturnsEmptyCollection()
    {
        var collection = "".Create();
        Assert.Empty(collection.Collection);
    }

    [Fact]
    public void Create_WhitespaceString_ReturnsEmptyCollection()
    {
        var collection = "   ".Create();
        Assert.Empty(collection.Collection);
    }

    [Fact]
    public void Create_SingleFieldNoDirection_AscendingByDefault()
    {
        var collection = "fieldName".Create();
        Assert.Single(collection.Collection);
        var ob = collection.Collection.First();
        Assert.Equal("fieldName", ob.FieldName);
        Assert.Equal(Direction.Ascending, ob.Direction);
    }

    [Fact]
    public void Create_SingleFieldDesc_IsDescending()
    {
        var collection = "fieldName DESC".Create();
        Assert.Single(collection.Collection);
        Assert.Equal(Direction.Descending, collection.Collection.First().Direction);
    }

    [Fact]
    public void Create_SingleFieldAsc_IsAscending()
    {
        var collection = "fieldName ASC".Create();
        Assert.Single(collection.Collection);
        Assert.Equal(Direction.Ascending, collection.Collection.First().Direction);
    }

    [Fact]
    public void Create_TwoFields_ReturnsTwoEntries()
    {
        var collection = "alpha,beta DESC".Create();
        Assert.Equal(2, collection.Collection.Count());
    }

    [Fact]
    public void Create_TwoFields_FirstIsAscending()
    {
        var collection = "alpha,beta DESC".Create();
        var first = collection.Collection.First();
        Assert.Equal("alpha", first.FieldName);
        Assert.Equal(Direction.Ascending, first.Direction);
    }

    [Fact]
    public void Create_TwoFields_SecondIsDescending()
    {
        var collection = "alpha,beta DESC".Create();
        var second = collection.Collection.Skip(1).First();
        Assert.Equal("beta", second.FieldName);
        Assert.Equal(Direction.Descending, second.Direction);
    }

    [Fact]
    public void Create_FieldNameSingleChar_IsIgnored()
    {
        var collection = "x".Create();
        Assert.Empty(collection.Collection);
    }

    [Fact]
    public void Create_MixedValidAndShortNames_IgnoresShortName()
    {
        var collection = "ab,x,cd".Create();
        Assert.Equal(2, collection.Collection.Count());
    }
}

// ── OrderByCollection.Apply on IQueryable ─────────────────────────────────────

public class OrderByCollectionApplyTests
{
    private sealed record Item(string Name, int Score);

    [Fact]
    public void Apply_SingleAscendingField_SortsByFieldAscending()
    {
        var items = new List<Item>
        {
            new("Banana", 2),
            new("Apple",  1),
            new("Cherry", 3)
        }.AsQueryable();

        var collection = "Name".Create();
        var sorted = collection.Apply(items).ToList();

        Assert.Equal("Apple", sorted[0].Name);
        Assert.Equal("Banana", sorted[1].Name);
        Assert.Equal("Cherry", sorted[2].Name);
    }

    [Fact]
    public void Apply_DescendingField_SortsByFieldDescending()
    {
        var items = new List<Item>
        {
            new("Banana", 2),
            new("Apple",  1),
            new("Cherry", 3)
        }.AsQueryable();

        var collection = "Score DESC".Create();
        var sorted = collection.Apply(items).ToList();

        Assert.Equal(3, sorted[0].Score);
        Assert.Equal(2, sorted[1].Score);
        Assert.Equal(1, sorted[2].Score);
    }

    [Fact]
    public void Apply_EmptyCollection_ReturnsSameOrder()
    {
        var items = new List<Item> { new("B", 2), new("A", 1) }.AsQueryable();
        var sorted = new OrderByCollection().Apply(items).ToList();
        Assert.Equal("B", sorted[0].Name); // order unchanged
        Assert.Equal("A", sorted[1].Name);
    }

    [Fact]
    public void Apply_OnIEnumerable_SortsCorrectly()
    {
        var items = new List<Item> { new("B", 2), new("A", 1) };
        var collection = "Name".Create();
        var sorted = collection.Apply((IEnumerable<Item>)items).ToList();
        Assert.Equal("A", sorted[0].Name);
        Assert.Equal("B", sorted[1].Name);
    }
}
