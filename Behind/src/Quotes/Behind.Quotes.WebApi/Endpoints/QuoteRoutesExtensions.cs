using Behind.Common.WebApi;
using Behind.Common.WebApi.Filters;
using Behind.Quotes.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Quotes.WebApi.Endpoints;

public static class QuoteRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapQuoteEndpoints()
        {
            application.MapGet("/quotes", QuoteEndpoints.List)
                .Produces<QuotesResponse>()
                .Produces(500);

            application.MapGet("/quotes/manage", QuoteEndpoints.Manage)
                .RequireAuthorization(QuotesModuleNames.ReadGrant)
                .Produces<QuoteManageResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(500);

            application.MapPost("/quotes", QuoteEndpoints.Create)
                .RequireAuthorization(QuotesModuleNames.WriteGrant)
                .AddEndpointFilter<DataAnnotationValidationFilter<QuoteEndpoints.SaveQuoteRequest>>()
                .Produces<QuoteSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces<QuoteConflictResponse>(409)
                .Produces(500);

            application.MapPost("/quotes/{id:guid}", QuoteEndpoints.Update)
                .RequireAuthorization(QuotesModuleNames.WriteGrant)
                .AddEndpointFilter<DataAnnotationValidationFilter<QuoteEndpoints.SaveQuoteRequest>>()
                .Produces<QuoteSavedResponse>()
                .Produces(400)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces<QuoteConflictResponse>(409)
                .Produces(500);

            application.MapPost("/quotes/{id:guid}/delete", QuoteEndpoints.Delete)
                .RequireAuthorization(QuotesModuleNames.DeleteGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/quotes/{id:guid}/restore", QuoteEndpoints.Restore)
                .RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant)
                .Produces(204)
                .Produces(401)
                .Produces(403)
                .Produces(404)
                .Produces(500);

            application.MapPost("/quotes/{id:guid}/hard/delete", QuoteEndpoints.HardDelete)
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
