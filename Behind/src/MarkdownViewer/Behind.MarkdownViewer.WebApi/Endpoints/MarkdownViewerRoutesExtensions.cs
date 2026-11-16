using Behind.MarkdownViewer.Common;
using Behind.MarkdownViewer.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.MarkdownViewer.WebApi.Endpoints;

public static class MarkdownViewerRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapMarkdownViewerEndpoints()
        {
            application.MapGet("/markdownviewer/documents", MarkdownViewerEndpoints.List)
                .Produces<MarkdownDocumentsResponse>()
                .Produces(500);

            application.MapGet("/markdownviewer/documents/{**path}", MarkdownViewerEndpoints.Page)
                .Produces<MarkdownPage>()
                .Produces(404)
                .Produces(500);

            application.MapGet("/markdownviewer/files/{**path}", MarkdownViewerEndpoints.Asset)
                .Produces(200)
                .Produces(404)
                .Produces(500);

            application.MapPost("/markdownviewer/generate", MarkdownViewerEndpoints.Generate)
                .RequireAuthorization(MarkdownViewerModuleNames.WriteGrant)
                .Produces<GeneratedSite>()
                .Produces<MarkdownProblemsResponse>(400)
                .Produces(401)
                .Produces(403)
                .Produces<MarkdownProblemsResponse>(500);
        }
    }
}
