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
        }
    }
}
