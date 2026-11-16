namespace Behind.Tests.CommonDatabase;

public sealed class EntityMarkRegistryTests
{
    [Fact]
    public void IsMarked_AfterMark_IsTrue()
    {
        var registry = new EntityMarkRegistry();
        var entity = new Grant("read");

        registry.Mark(entity);

        Assert.True(registry.IsMarked(entity));
    }

    [Fact]
    public void IsMarked_EqualButDifferentInstance_IsFalse()
    {
        var registry = new EntityMarkRegistry();

        registry.Mark(new Grant("read"));

        Assert.False(registry.IsMarked(new Grant("read")));
    }

    [Fact]
    public void Clear_RemovesAllMarks()
    {
        var registry = new EntityMarkRegistry();
        var entity = new Grant("read");
        registry.Mark(entity);

        registry.Clear();

        Assert.False(registry.IsMarked(entity));
    }
}
