using Behind.Common.WebApi;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public sealed record MenuItemResponse(
    Guid Id,
    string Title,
    string Url,
    MenuTarget Target,
    MenuItemKind Kind,
    IReadOnlyList<MenuItemResponse> Children);

public sealed record MenuResponse(Guid Id, string Name, string Title, IReadOnlyList<MenuItemResponse> Items);

public sealed record MenusResponse(IReadOnlyList<MenuResponse> Menus);

public sealed record ManagedMenu(Guid Id, string Name, string Title, long ItemCount, bool IsBuiltIn, bool IsDeleted);

public sealed record MenuManageResponse(PageResponse<ManagedMenu> Menus, IReadOnlyList<string> Letters);

public sealed record ManagedMenuItem(
    Guid Id,
    Guid? ParentId,
    string Title,
    string Url,
    MenuTarget Target,
    MenuItemKind Kind,
    int Position,
    IReadOnlyList<Guid> RoleIds,
    bool IsDeleted);

public sealed record MenuRoleItem(Guid Id, string Name);

public sealed record ManagedMenuParent(Guid Id, string Title);

public sealed record MenuItemsManageResponse(
    ManagedMenu Menu,
    bool AllowsUserItems,
    PageResponse<ManagedMenuItem> Items,
    IReadOnlyList<string> Letters,
    IReadOnlyList<ManagedMenuParent> Parents,
    IReadOnlyList<MenuRoleItem> Roles);
