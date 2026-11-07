namespace Behind.Tests.Common.Modules;

public class DeterministicGuidExtensionsTests
{
    [Fact]
    public void FromName_IsStableForTheSameInput()
    {
        Assert.Equal(Guid.FromName("61000000", "Notes.Read"), Guid.FromName("61000000", "Notes.Read"));
    }

    [Fact]
    public void FromName_DiffersByName()
    {
        Assert.NotEqual(Guid.FromName("61000000", "Notes.Read"), Guid.FromName("61000000", "Notes.Write"));
    }

    [Fact]
    public void FromName_KeepsThePrefix()
    {
        Assert.StartsWith("61000000-", Guid.FromName("61000000", "Notes.Read").ToString());
    }
}
