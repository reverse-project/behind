using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class WidgetRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapWidgetEndpoints()
        {
            var widgets = application.MapGroup("/widgets").WithTags("Widgets");
            widgets.MapGet("/chat", ChatEndpoints.List)
                .WithSummary("List chat history; guests may read, signed-in users require Chat.Read.")
                .Produces<ChatEndpoints.Page>().Produces(400).Produces(403);
            widgets.MapGet("/chat/{id:guid}", ChatEndpoints.Get)
                .WithSummary("Read a visible chat message; signed-in users require Chat.Read.")
                .Produces<ChatEndpoints.Message>().Produces(403).Produces(404);
            widgets.MapPost("/chat", ChatEndpoints.Add)
                .WithSummary("Post an authenticated chat message.")
                .Produces(201).Produces(400).Produces(401).Produces(403).Produces(409)
                .RequireAuthorization(ChatEndpoints.ReadGrant, ChatEndpoints.WriteGrant);
            widgets.MapPut("/chat/{id:guid}", ChatEndpoints.Update)
                .WithSummary("Edit your own message within five minutes of creation.")
                .Produces(204).Produces(400).Produces(401).Produces(403).Produces(404).Produces(409)
                .RequireAuthorization(ChatEndpoints.ReadGrant, ChatEndpoints.WriteGrant);
            widgets.MapDelete("/chat/{id:guid}", ChatEndpoints.Delete)
                .WithSummary("Remove your own message with Chat.Delete, or moderate any message with Chat.Manage.")
                .Produces(204).Produces(401).Produces(403).Produces(404).RequireAuthorization(ChatEndpoints.ReadGrant);
            widgets.MapPost("/sanitize", WidgetEndpoints.Sanitize)
                .WithSummary("Sanitize widget rich text.").Produces<WidgetEndpoints.HtmlResponse>().Produces(400);
            widgets.MapGet("/downloads", WidgetEndpoints.LatestDownloads)
                .WithSummary("List recent public downloads.").Produces<DownloadFilesResponse>();
            widgets.MapGet("/news", WidgetEndpoints.LatestNews)
                .WithSummary("List live news in newest creation order.").Produces<IReadOnlyList<NewsListItem>>();
            widgets.MapGet("/messages/{id:guid}", MessageEndpoints.Live)
                .WithSummary("Read a scheduled message while it is live.")
                .Produces<MessageWidgetResponse>().Produces(404);
        }
    }
}
