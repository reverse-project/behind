using Behind.Core.Domain;

namespace Behind.Core.Database;

public static class MenuTreeExtensions
{
    extension(IEnumerable<IMenuItemQueries.MenuItemListing> items)
    {
        public IReadOnlyList<IMenuQueries.MenuItemView> ToVisibleTree(IReadOnlySet<Guid> viewerRoleIds)
        {
            var visible = items.Where(item => item.RoleIds.IsVisibleTo(viewerRoleIds)).ToList();

            var childrenOf = visible
                .Where(item => item.ParentId.Match(() => false, _ => true))
                .ToLookup(item => item.ParentId.Match(() => Guid.Empty, parentId => parentId));

            return visible
                .Where(item => item.ParentId.Match(() => true, _ => false))
                .Ordered()
                .Select(item => item.ToView(childrenOf[item.Id.Key].Ordered().Select(child => child.ToView([])).ToList()))
                .ToList();
        }

        private IEnumerable<IMenuItemQueries.MenuItemListing> Ordered() =>
            items.OrderBy(item => item.Position).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase);
    }

    extension(IMenuItemQueries.MenuItemListing item)
    {
        private IMenuQueries.MenuItemView ToView(IReadOnlyList<IMenuQueries.MenuItemView> children) =>
            new(item.Id.Key, item.Title, item.Url, item.Target, item.Kind, children);
    }
}
