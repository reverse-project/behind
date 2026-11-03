using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class MenuResponseExtensions
{
    public const string BuiltInReason = "built-in";
    public const string UnknownMenuReason = "unknown-menu";
    public const string InvalidParentReason = "invalid-parent";
    public const string KindNotAllowedReason = "kind-not-allowed";
    public const string UnknownRoleReason = "unknown-role";

    extension(IMenuQueries.MenuItemView item)
    {
        public MenuItemResponse ToResponse() =>
            new(item.Id, item.Title, item.Url, item.Target, item.Kind, item.Children.Select(child => child.ToResponse()).ToList());
    }

    extension(IMenuQueries.MenuView menu)
    {
        public MenuResponse ToResponse() =>
            new(menu.Id, menu.Name, menu.Title, menu.Items.Select(item => item.ToResponse()).ToList());
    }

    extension(IMenuQueries.MenuListing menu)
    {
        public ManagedMenu ToItem(DeletionState state) =>
            new(
                menu.Id.Key,
                menu.Name,
                menu.Title,
                menu.ItemCount,
                BuiltInMenus.IsBuiltIn(menu.Id.Key),
                state == DeletionState.Deleted);
    }

    extension(IMenuItemQueries.MenuItemListing item)
    {
        public ManagedMenuItem ToItem(DeletionState state) =>
            new(
                item.Id.Key,
                item.ParentId.ToNullable(),
                item.Title,
                item.Url,
                item.Target,
                item.Kind,
                item.Position,
                item.RoleIds,
                state == DeletionState.Deleted);
    }

    extension(MenuSaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                MenuSaveResult.Saved saved => Results.Json(new NewsSavedResponse(saved.Id.Key, saved.Id.Version)),
                MenuSaveResult.NotFound => Results.NotFound(),
                MenuSaveResult.NameTaken => Conflict(NewsResponseExtensions.NameTakenReason),
                MenuSaveResult.BuiltIn => Conflict(BuiltInReason),
                _ => Conflict(NewsResponseExtensions.VersionConflictReason)
            };
    }

    extension(MenuRemovalResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                MenuRemovalResult.Removed => Results.NoContent(),
                MenuRemovalResult.NotFound => Results.NotFound(),
                _ => Conflict(BuiltInReason)
            };
    }

    extension(MenuItemSaveResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                MenuItemSaveResult.Saved saved => Results.Json(new NewsSavedResponse(saved.Id.Key, saved.Id.Version)),
                MenuItemSaveResult.NotFound => Results.NotFound(),
                MenuItemSaveResult.UnknownMenu => Conflict(UnknownMenuReason),
                MenuItemSaveResult.InvalidParent => Conflict(InvalidParentReason),
                MenuItemSaveResult.KindNotAllowed => Conflict(KindNotAllowedReason),
                MenuItemSaveResult.UnknownRole => Conflict(UnknownRoleReason),
                _ => Conflict(NewsResponseExtensions.VersionConflictReason)
            };
    }

    private static IResult Conflict(string reason) => Results.Conflict(new NewsConflictResponse(reason));
}
