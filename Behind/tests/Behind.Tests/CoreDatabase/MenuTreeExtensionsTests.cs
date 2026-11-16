using Behind.Core.Database;

namespace Behind.Tests.CoreDatabase;

public sealed class MenuTreeExtensionsTests
{
    private static readonly Guid Editors = Guid.NewGuid();
    private static readonly IReadOnlySet<Guid> Nobody = new HashSet<Guid>();
    private static readonly IReadOnlySet<Guid> EditorSet = new HashSet<Guid> { Editors };

    private static IMenuItemQueries.MenuItemListing Item(
        string title,
        int position = 0,
        Option<Guid> parent = default,
        params Guid[] roles) =>
        new(
            new VersionedKey<Guid>(Guid.NewGuid(), 1),
            Guid.Empty,
            parent,
            title,
            "#/x",
            MenuTarget.CurrentPage,
            MenuItemKind.Link,
            position,
            roles);

    [Fact]
    public void ToVisibleTree_OrdersTopLevelItemsByPositionThenTitle()
    {
        var tree = new[] { Item("b", 10), Item("c", 5), Item("A", 10) }.ToVisibleTree(Nobody);

        Assert.Equal(["c", "A", "b"], tree.Select(item => item.Title));
    }

    [Fact]
    public void ToVisibleTree_NestsSubItemsUnderTheirParent()
    {
        var group = Item("Group");
        var tree = new[]
        {
            Item("Second", 20, group.Id.Key),
            Item("First", 10, group.Id.Key),
            group
        }.ToVisibleTree(Nobody);

        var root = Assert.Single(tree);
        Assert.Equal(["First", "Second"], root.Children.Select(child => child.Title));
        Assert.Equal(group.Id.Key, root.Id);
    }

    [Fact]
    public void ToVisibleTree_HidesItemsOfOtherRolesAndTheirSubItems()
    {
        var hidden = Item("Hidden", roles: Editors);
        var tree = new[]
        {
            hidden,
            Item("Child of hidden", parent: hidden.Id.Key),
            Item("Shown")
        }.ToVisibleTree(Nobody);

        Assert.Equal(["Shown"], tree.Select(item => item.Title));
        Assert.Equal(["Hidden", "Shown"], new[] { hidden, Item("Shown") }.ToVisibleTree(EditorSet).Select(item => item.Title));
    }

    [Fact]
    public void ToVisibleTree_OrphanedAndDeeperSubItemsAreNotShown()
    {
        var group = Item("Group");
        var child = Item("Child", parent: group.Id.Key);
        var tree = new[]
        {
            group,
            child,
            Item("Grandchild", parent: child.Id.Key),
            Item("Orphan", parent: Guid.NewGuid())
        }.ToVisibleTree(Nobody);

        var root = Assert.Single(tree);
        Assert.Equal(["Child"], root.Children.Select(item => item.Title));
        Assert.Empty(root.Children[0].Children);
    }

    [Fact]
    public void ToVisibleTree_NoItems_IsEmpty()
    {
        Assert.Empty(Array.Empty<IMenuItemQueries.MenuItemListing>().ToVisibleTree(Nobody));
    }
}
