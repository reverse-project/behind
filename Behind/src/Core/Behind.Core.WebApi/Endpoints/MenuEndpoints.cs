using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class MenuEndpoints
{
    public const int DefaultPageSize = ManagedListExtensions.DefaultPageSize;
    public const int MaxPageSize = ManagedListExtensions.MaxPageSize;
    public const int MaxMenusPerRequest = 10;
    public const int MaxRolesPerItem = 100;
    public const int MaxPosition = 1_000_000;

    public sealed record SaveMenuRequest(
        [property: Required, MaxLength(Menu.MaxNameLength * 2)] string Name,
        [property: Required, MaxLength(Menu.MaxTitleLength)] string Title);

    public sealed record SaveMenuItemRequest(
        [property: Required, MaxLength(MenuItem.MaxTitleLength)] string Title,
        [property: Required, MaxLength(MenuItem.MaxUrlLength)] string Url,
        MenuTarget Target,
        MenuItemKind Kind,
        [property: Range(-MaxPosition, MaxPosition)] int Position,
        Guid? ParentId,
        [property: MaxLength(MaxRolesPerItem)] IReadOnlyList<Guid>? RoleIds);

    [IgnoreETag]
    public static async Task<IResult> Resolve(
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        IMenuQueries queries,
        [FromQuery(Name = "name")] string[]? names = null)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var wanted = (names ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (wanted.Count == 0 || wanted.Count > MaxMenusPerRequest)
        {
            return Results.BadRequest();
        }

        var viewer = (await httpContext.FindCurrentSession(sessionQueries)).Map(session => session.UserId);
        var menus = await queries.Resolve(wanted, viewer, httpContext.RequestAborted);

        return Results.Json(new MenusResponse(menus.Select(menu => menu.ToResponse()).ToList()));
    }

    [IgnoreETag]
    public static Task<IResult> Manage(
        HttpContext httpContext,
        IMenuQueries queries,
        IAuthorizationService authorization,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? search = null,
        [FromQuery] string? letter = null,
        [FromQuery] bool deleted = false) =>
        httpContext.WithManagedList(
            authorization,
            search,
            letter,
            deleted,
            async query =>
            {
                var menus = await queries.List(
                    Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                    query.Search,
                    query.Letter,
                    query.State,
                    httpContext.RequestAborted);

                var letters = await queries.GetLetters(query.Search, query.State, httpContext.RequestAborted);

                return Results.Json(new MenuManageResponse(
                    menus.ToPageResponse(menu => menu.ToItem(query.State)),
                    letters));
            });

    [IgnoreETag]
    public static Task<IResult> ManageItems(
        Guid id,
        HttpContext httpContext,
        IMenuQueries menus,
        IMenuItemQueries items,
        IRoleQueries roles,
        IAuthorizationService authorization,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? search = null,
        [FromQuery] string? letter = null,
        [FromQuery] bool deleted = false) =>
        httpContext.WithManagedList(
            authorization,
            search,
            letter,
            deleted,
            async query =>
            {
                var menu = await FindMenu(id, httpContext, menus, authorization);

                return await menu.Match(
                    () => Task.FromResult(Results.NotFound()),
                    async found =>
                    {
                        var listed = await items.ListOfMenu(
                            id,
                            Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                            query.Search,
                            query.Letter,
                            query.State,
                            httpContext.RequestAborted);

                        var letters = await items.GetLetters(id, query.Search, query.State, httpContext.RequestAborted);
                        var parents = await items.ListParents(id, httpContext.RequestAborted);
                        var available = await roles.ListRoles(httpContext.RequestAborted);

                        return Results.Json(new MenuItemsManageResponse(
                            found,
                            BuiltInMenus.IsMain(id),
                            listed.ToPageResponse(item => item.ToItem(query.State)),
                            letters,
                            parents.Select(parent => new ManagedMenuParent(parent.Id.Key, parent.Title)).ToList(),
                            available.Select(role => new MenuRoleItem(role.Id, role.Role.Name)).ToList()));
                    });
            });

    [IgnoreETag]
    public static Task<IResult> Create(
        SaveMenuRequest request,
        HttpContext httpContext,
        IMenuCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Create(draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static Task<IResult> Update(
        Guid id,
        SaveMenuRequest request,
        HttpContext httpContext,
        IMenuCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Update(id, draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid id, HttpContext httpContext, IMenuCommands commands) =>
        (await commands.Delete(id, httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, IMenuCommands commands)
    {
        var result = await commands.Restore(id, httpContext.RequestAborted);

        return result is MenuSaveResult.Saved ? Results.NoContent() : result.ToResult();
    }

    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid id, HttpContext httpContext, IMenuCommands commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static Task<IResult> CreateItem(
        Guid id,
        SaveMenuItemRequest request,
        HttpContext httpContext,
        IMenuItemCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Create(id, draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static Task<IResult> UpdateItem(
        Guid itemId,
        SaveMenuItemRequest request,
        HttpContext httpContext,
        IMenuItemCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Update(itemId, draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static async Task<IResult> DeleteItem(Guid itemId, HttpContext httpContext, IMenuItemCommands commands) =>
        await commands.Delete(itemId, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> RestoreItem(Guid itemId, HttpContext httpContext, IMenuItemCommands commands) =>
        await commands.Restore(itemId, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> HardDeleteItem(
        Guid itemId,
        HttpContext httpContext,
        IMenuItemCommands commands) =>
        (await commands.HardDelete(itemId, httpContext.RequestAborted)).ToResult();

    private static async Task<Option<ManagedMenu>> FindMenu(
        Guid id,
        HttpContext httpContext,
        IMenuQueries menus,
        IAuthorizationService authorization)
    {
        var active = await menus.Find(id, DeletionState.Active, httpContext.RequestAborted);

        if (active.Match(() => false, _ => true))
        {
            return active.Map(menu => menu.ToItem(DeletionState.Active));
        }

        var canViewDeleted = await authorization.HoldsGrantAsync(
            httpContext.User,
            ManagedListExtensions.DeletedRecordsGrant);

        return canViewDeleted
            ? (await menus.Find(id, DeletionState.Deleted, httpContext.RequestAborted))
                .Map(menu => menu.ToItem(DeletionState.Deleted))
            : None;
    }
}
