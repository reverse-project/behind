using Behind.Common;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public static class MenuDraftExtensions
{
    extension(MenuEndpoints.SaveMenuRequest request)
    {
        public Option<IMenuCommands.MenuDraft> ToDraft()
        {
            var title = (request.Title ?? string.Empty).Trim();

            return (request.Name ?? string.Empty).ToSlug(Menu.MaxNameLength).Bind(name =>
                title.Length == 0 ? None : Some(new IMenuCommands.MenuDraft(name, title)));
        }
    }

    extension(MenuEndpoints.SaveMenuItemRequest request)
    {
        public Option<IMenuItemCommands.MenuItemDraft> ToDraft()
        {
            var title = (request.Title ?? string.Empty).Trim();
            var url = (request.Url ?? string.Empty).Trim();

            return title.Length == 0 || !url.IsSafeMenuUrl
                ? None
                : Some(new IMenuItemCommands.MenuItemDraft(
                    title,
                    url,
                    request.Target,
                    request.Kind,
                    request.Position,
                    request.ParentId.ToOption(),
                    (request.RoleIds ?? []).Distinct().ToList()));
        }
    }
}
