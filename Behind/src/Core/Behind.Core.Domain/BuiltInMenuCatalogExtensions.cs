using Behind.Common.Modules;

namespace Behind.Core.Domain;

public static class BuiltInMenuCatalogExtensions
{
    extension(ModuleMenuItem item)
    {
        public BuiltInMenuItem ToBuiltIn() =>
            new(
                item.Id,
                item.Title,
                item.Url,
                item.Kind == ModuleMenuKind.User ? MenuItemKind.User : MenuItemKind.Link,
                item.Position,
                item.Audience.RoleIds);
    }

    extension(ModuleAudience audience)
    {
        private IReadOnlyList<Guid> RoleIds =>
            audience switch
            {
                ModuleAudience.Administration => [BuiltInAccess.Administrators.Id, BuiltInAccess.Developers.Id],
                ModuleAudience.Developer => [BuiltInAccess.Developers.Id],
                _ => []
            };
    }
}
