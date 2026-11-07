using Behind.Common.WebApi.Filters;
using Behind.Core.Domain;
using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class AccessRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapAccessEndpoints()
        {
            var read = GrantName.Of(GrantArea.Access, GrantAction.Read);
            var write = GrantName.Of(GrantArea.Access, GrantAction.Write);
            var delete = GrantName.Of(GrantArea.Access, GrantAction.Delete);

            application.MapGet(AccessGateExtensions.RestrictionRoute, AccessEndpoints.Restriction)
                .Produces<RestrictionStatusResponse>()
                .Produces(500);

            application.MapGet($"{VfsTypes.AccessImageUrl}/{{id:guid}}", SiteImageEndpoints.GetAccess)
                .Produces(200)
                .Produces(404)
                .Produces(500);

            application.MapPost(VfsTypes.AccessImageUrl, SiteImageEndpoints.UploadAccess)
                .RequireAuthorization(write)
                .DisableAntiforgery()
                .Produces<SiteImageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<SiteImageLimitResponse>(413)
                .Produces(415)
                .Produces(500);

            application.MapGet("/access/manage", AccessEndpoints.Manage)
                .RequireAuthorization(read)
                .Produces<AccessManageResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/access", AccessEndpoints.Save)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<AccessEndpoints.SaveAccessSettingsRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/access/delete", AccessEndpoints.Delete)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/access/restore", AccessEndpoints.Restore)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/access/hard/delete", AccessEndpoints.HardDelete)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(409)
                .Produces(500);

            application.MapGet("/access/pending-users", AccessEndpoints.PendingUsers)
                .RequireAuthorization(read)
                .Produces<PendingUsersResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapPost("/access/pending-users/{id:guid}/approve", AccessEndpoints.Approve)
                .RequireAuthorization(write)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/access/pending-users/{id:guid}/reject", AccessEndpoints.Reject)
                .RequireAuthorization(write)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);
        }
    }
}
