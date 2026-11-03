using Behind.Common;
using Behind.Core.Database.Commands;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database;

public static class BuiltInMenusExtensions
{
    extension(AbstractCoreContext context)
    {
        public async Task EnsureBuiltInMenusAsync(CancellationToken cancellationToken = default)
        {
            var menus = new MenuCommands(context);
            var items = new MenuItemCommands(context);

            foreach (var menu in BuiltInMenus.All)
            {
                await menus.Ensure(menu.Id, new IMenuCommands.MenuDraft(menu.Name, menu.Title), cancellationToken);

                foreach (var item in menu.Items)
                {
                    await items.Ensure(
                        item.Id,
                        menu.Id,
                        new IMenuItemCommands.MenuItemDraft(
                            item.Title,
                            item.Url,
                            MenuTarget.CurrentPage,
                            item.Kind,
                            item.Position,
                            None,
                            item.RoleIds),
                        cancellationToken);
                }
            }
        }
    }
}
