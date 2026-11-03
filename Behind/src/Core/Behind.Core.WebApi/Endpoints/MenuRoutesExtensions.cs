using Behind.Common.WebApi.Filters;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class MenuRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapMenuEndpoints()
        {
            var read = GrantName.Of(GrantArea.Menu, GrantAction.Read);
            var write = GrantName.Of(GrantArea.Menu, GrantAction.Write);
            var delete = GrantName.Of(GrantArea.Menu, GrantAction.Delete);

            application.MapGet("/menus", MenuEndpoints.Resolve)
                .Produces<MenusResponse>()
                .Produces(400)
                .Produces(500);

            application.MapGet("/menus/manage", MenuEndpoints.Manage)
                .RequireAuthorization(read)
                .Produces<MenuManageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapGet("/menus/{id:guid}/items/manage", MenuEndpoints.ManageItems)
                .RequireAuthorization(read)
                .Produces<MenuItemsManageResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/menus", MenuEndpoints.Create)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<MenuEndpoints.SaveMenuRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/menus/{id:guid}", MenuEndpoints.Update)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<MenuEndpoints.SaveMenuRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/menus/{id:guid}/delete", MenuEndpoints.Delete)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/menus/{id:guid}/restore", MenuEndpoints.Restore)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/menus/{id:guid}/hard/delete", MenuEndpoints.HardDelete)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(500);

            application.MapPost("/menus/{id:guid}/items", MenuEndpoints.CreateItem)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<MenuEndpoints.SaveMenuItemRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/menus/items/{itemId:guid}", MenuEndpoints.UpdateItem)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<MenuEndpoints.SaveMenuItemRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/menus/items/{itemId:guid}/delete", MenuEndpoints.DeleteItem)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/menus/items/{itemId:guid}/restore", MenuEndpoints.RestoreItem)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/menus/items/{itemId:guid}/hard/delete", MenuEndpoints.HardDeleteItem)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(500);
        }
    }
}
