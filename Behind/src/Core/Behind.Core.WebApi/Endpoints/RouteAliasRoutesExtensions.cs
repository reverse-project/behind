using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class RouteAliasRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapRouteAliasEndpoints()
        {
            var read = GrantName.Of(GrantArea.RouteAlias, GrantAction.Read);
            var write = GrantName.Of(GrantArea.RouteAlias, GrantAction.Write);
            var delete = GrantName.Of(GrantArea.RouteAlias, GrantAction.Delete);

            application.MapGet("/route-aliases/resolve/{alias}", RouteAliasEndpoints.Resolve)
                .Produces<RouteAliasTargetResponse>()
                .Produces(404)
                .Produces(500);

            application.MapGet("/route-aliases/manage", RouteAliasEndpoints.Manage)
                .RequireAuthorization(read)
                .Produces<RouteAliasManageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapPost("/route-aliases", RouteAliasEndpoints.Create)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<RouteAliasEndpoints.SaveRouteAliasRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/route-aliases/{id:guid}", RouteAliasEndpoints.Update)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<RouteAliasEndpoints.SaveRouteAliasRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/route-aliases/{id:guid}/delete", RouteAliasEndpoints.Delete)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/route-aliases/{id:guid}/restore", RouteAliasEndpoints.Restore)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/route-aliases/{id:guid}/hard/delete", RouteAliasEndpoints.HardDelete)
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
