using Behind.Common.WebApi.Filters;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class ReportRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapReportEndpoints()
        {
            application.MapPost("/reports", ReportEndpoints.Create)
                .RequireAuthorization(GrantName.Of(GrantArea.Report, GrantAction.Write))
                .AddEndpointFilter<DataAnnotationValidationFilter<ReportEndpoints.CreateRequest>>()
                .Produces(201).Produces(400).Produces(401).Produces(403).Produces(404);

            var manage = application.MapGroup("/reports")
                .RequireAuthorization(GrantName.Of(GrantArea.Report, GrantAction.Manage));
            manage.MapGet("", ReportEndpoints.List).Produces(200).Produces(400).Produces(401).Produces(403);
            manage.MapGet("/{id:guid}", ReportEndpoints.Get).Produces(200).Produces(401).Produces(403).Produces(404);
            manage.MapGet("/{id:guid}/conversation", ReportEndpoints.Conversation)
                .WithSummary("Read only the private conversation attached to this report.")
                .Produces<PrivateMessageStore.Conversation>().Produces(401).Produces(403).Produces(404);
            manage.MapPost("/{id:guid}/resolve", ReportEndpoints.Resolve)
                .AddEndpointFilter<DataAnnotationValidationFilter<ReportEndpoints.ResolveRequest>>()
                .Produces(200).Produces(400).Produces(401).Produces(403).Produces(404).Produces(409);
            manage.MapPost("/{id:guid}/retry-email", ReportEndpoints.Retry)
                .Produces(200).Produces(401).Produces(403).Produces(404).Produces(409);
            manage.MapPost("/{id:guid}/delete", ReportEndpoints.Delete).Produces(204).Produces(404);
            manage.MapPost("/{id:guid}/restore", ReportEndpoints.Delete)
                .RequireAuthorization("RawDatabase.Manage").Produces(204).Produces(403).Produces(404);
            manage.MapPost("/{id:guid}/hard/delete", ReportEndpoints.Delete)
                .RequireAuthorization("RawDatabase.Manage").Produces(204).Produces(403).Produces(404);
        }
    }
}
