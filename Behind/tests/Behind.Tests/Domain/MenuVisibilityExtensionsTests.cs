namespace Behind.Tests.Domain;

public class MenuVisibilityExtensionsTests
{
    private static readonly Guid Editors = Guid.NewGuid();
    private static readonly Guid Readers = Guid.NewGuid();

    [Fact]
    public void IsVisibleTo_NoRoles_IsVisibleToEveryone()
    {
        IReadOnlyCollection<Guid> none = [];

        Assert.True(none.IsVisibleTo(new HashSet<Guid>()));
        Assert.True(none.IsVisibleTo(new HashSet<Guid> { Editors }));
    }

    [Fact]
    public void IsVisibleTo_Roles_NeedsAtLeastOneOfThem()
    {
        IReadOnlyCollection<Guid> roles = [Editors, Readers];

        Assert.True(roles.IsVisibleTo(new HashSet<Guid> { Readers }));
        Assert.True(roles.IsVisibleTo(new HashSet<Guid> { Editors, Readers, Guid.NewGuid() }));
        Assert.False(roles.IsVisibleTo(new HashSet<Guid> { Guid.NewGuid() }));
        Assert.False(roles.IsVisibleTo(new HashSet<Guid>()));
    }
}
