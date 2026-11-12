using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class MessageRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapMessageEndpoints()
        {
            var read = GrantName.Of(GrantArea.Message, GrantAction.Read);
            var write = GrantName.Of(GrantArea.Message, GrantAction.Write);
            var delete = GrantName.Of(GrantArea.Message, GrantAction.Delete);

            application.MapGet("/messages/manage", MessageEndpoints.Manage)
                .RequireAuthorization(read)
                .Produces<MessageManageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapGet("/messages/{id:guid}", MessageEndpoints.Get)
                .RequireAuthorization(read)
                .Produces<MessageDetailsResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/messages", MessageEndpoints.Create)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<MessageEndpoints.SaveMessageRequest>>()
                .Produces<VersionedSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<VersionedConflictResponse>(409)
                .Produces(500);

            application.MapPost("/messages/{id:guid}", MessageEndpoints.Update)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<MessageEndpoints.SaveMessageRequest>>()
                .Produces<VersionedSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<VersionedConflictResponse>(409)
                .Produces(500);

            application.MapPost("/messages/{id:guid}/delete", MessageEndpoints.Delete)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/messages/{id:guid}/restore", MessageEndpoints.Restore)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/messages/{id:guid}/hard/delete", MessageEndpoints.HardDelete)
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
