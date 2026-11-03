using Behind.Common.WebApi.Filters;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class GalleryRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapGalleryEndpoints()
        {
            var read = GrantName.Of(GrantArea.Gallery, GrantAction.Read);
            var write = GrantName.Of(GrantArea.Gallery, GrantAction.Write);
            var delete = GrantName.Of(GrantArea.Gallery, GrantAction.Delete);

            application.MapGet("/galleries", GalleryEndpoints.List)
                .Produces<PublicGalleryListResponse>()
                .Produces(400)
                .Produces(500);

            application.MapGet("/galleries/manage", GalleryEndpoints.Manage)
                .RequireAuthorization(read)
                .Produces<GalleryManageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapGet("/galleries/{id:guid}/versions", GalleryEndpoints.GetHistory)
                .RequireAuthorization(read)
                .Produces<GalleryHistoryResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapGet("/galleries/{id:guid}/versions/{version:long}", GalleryEndpoints.GetVersion)
                .RequireAuthorization(read)
                .Produces<GalleryDetailResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapGet($"{GalleryImageEndpoints.ImagePath}/{{id:guid}}", GalleryImageEndpoints.Get)
                .Produces(200)
                .Produces(404)
                .Produces(500);

            application.MapGet("/galleries/{id:guid}", GalleryEndpoints.Get)
                .Produces<GalleryDetailResponse>()
                .Produces(404)
                .Produces(500);

            application.MapPost("/galleries/images", GalleryImageEndpoints.UploadImages)
                .RequireAuthorization(write)
                .DisableAntiforgery()
                .WithMetadata(new RequestSizeLimitAttribute(GalleryImageEndpoints.MaxRequestBytes))
                .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = GalleryImageEndpoints.MaxRequestBytes })
                .Produces<GalleryUploadResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapPost("/galleries/thumbnail", GalleryImageEndpoints.UploadThumbnail)
                .RequireAuthorization(write)
                .DisableAntiforgery()
                .Produces<GalleryImageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<GalleryImageLimitResponse>(413)
                .Produces(415)
                .Produces(500);

            application.MapPost("/galleries", GalleryEndpoints.Create)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<GalleryEndpoints.SaveGalleryRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/galleries/{id:guid}/versions", GalleryEndpoints.SaveVersion)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<GalleryEndpoints.SaveGalleryRequest>>()
                .Produces<NewsSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/galleries/{id:guid}/versions/{version:long}/activate", GalleryEndpoints.Activate)
                .RequireAuthorization(write)
                .Produces<NewsSavedResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<NewsConflictResponse>(409)
                .Produces(500);

            application.MapPost("/galleries/{id:guid}/delete", GalleryEndpoints.Delete)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/galleries/{id:guid}/restore", GalleryEndpoints.Restore)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/galleries/{id:guid}/hard/delete", GalleryEndpoints.HardDelete)
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
