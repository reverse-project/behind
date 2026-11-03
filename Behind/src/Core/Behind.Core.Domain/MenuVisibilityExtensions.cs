namespace Behind.Core.Domain;

public static class MenuVisibilityExtensions
{
    extension(IReadOnlyCollection<Guid> itemRoleIds)
    {
        public bool IsVisibleTo(IReadOnlySet<Guid> viewerRoleIds) =>
            itemRoleIds.Count == 0 || itemRoleIds.Any(viewerRoleIds.Contains);
    }
}
