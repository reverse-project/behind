using Behind.Core.Domain;
using Behind.Core.WebApi.Layouts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class LayoutRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapLayoutEndpoints()
        {
            application.MapGet("/layouts", LayoutEndpoints.Get)
                .WithSummary("Read active page layouts and route assignments.")
                .Produces<LayoutConfiguration>().Produces(500);
            application.MapGet("/layouts/manage", LayoutEndpoints.Manage)
                .RequireAuthorization(GrantName.Of(GrantArea.Configuration, GrantAction.Read))
                .WithSummary("Read layout configuration for administration.")
                .Produces<LayoutConfiguration>().Produces(401).Produces(403).Produces(500);
            application.MapPost("/layouts", LayoutEndpoints.Save)
                .RequireAuthorization(GrantName.Of(GrantArea.Configuration, GrantAction.Manage))
                .WithSummary("Save a validated layout configuration using its revision.")
                .Produces<LayoutConfiguration>().Produces(400).Produces(401).Produces(403).Produces(409).Produces(500);
            application.MapGet($"{VfsTypes.LayoutImageUrl}/{{id:guid}}", SiteImageEndpoints.GetLayout)
                .WithSummary("Read a custom layout widget image.")
                .Produces(200).Produces(404).Produces(500);
            application.MapPost(VfsTypes.LayoutImageUrl, SiteImageEndpoints.UploadLayout)
                .RequireAuthorization(GrantName.Of(GrantArea.Configuration, GrantAction.Manage))
                .DisableAntiforgery()
                .WithSummary("Upload an image for custom layout widget content.")
                .Produces<SiteImageResponse>().Produces(400).Produces(401).Produces(403)
                .Produces<SiteImageLimitResponse>(413).Produces(415).Produces(500);
        }
    }
}
