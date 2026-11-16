using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class CarouselRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapCarouselEndpoints()
        {
            var read = GrantName.Of(GrantArea.Site, GrantAction.Read);
            var write = GrantName.Of(GrantArea.Site, GrantAction.Write);
            var delete = GrantName.Of(GrantArea.Site, GrantAction.Delete);

            application.MapGet("/carousels/settings", CarouselEndpoints.GetSettings)
                .Produces<CarouselSettingsResponse>()
                .Produces(500);

            application.MapGet($"{VfsTypes.CarouselImageUrl}/{{id:guid}}", SiteImageEndpoints.GetCarousel)
                .Produces(200)
                .Produces(404)
                .Produces(500);

            application.MapPost(VfsTypes.CarouselImageUrl, SiteImageEndpoints.UploadCarousel)
                .RequireAuthorization(write)
                .DisableAntiforgery()
                .Produces<SiteImageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<SiteImageLimitResponse>(413)
                .Produces(415)
                .Produces(500);

            application.MapGet("/carousels/sites/{siteId:guid}", CarouselEndpoints.GetPublic)
                .Produces<CarouselResponse>()
                .Produces(404)
                .Produces(500);

            application.MapGet("/sites/{siteId:guid}/carousel", CarouselEndpoints.GetManaged)
                .RequireAuthorization(read)
                .Produces<CarouselResponse>()
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/sites/{siteId:guid}/carousel", CarouselEndpoints.Save)
                .RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<CarouselEndpoints.SaveCarouselRequest>>()
                .Produces<CarouselSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<CarouselConflictResponse>(409)
                .Produces(500);

            application.MapPost("/sites/{siteId:guid}/carousel/delete", CarouselEndpoints.Delete)
                .RequireAuthorization(delete)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/sites/{siteId:guid}/carousel/restore", CarouselEndpoints.Restore)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/sites/{siteId:guid}/carousel/hard/delete", CarouselEndpoints.HardDelete)
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
