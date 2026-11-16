using Behind.Common.WebApi.Filters;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Behind.Core.WebApi.Endpoints;

public static class PrivateMessageRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapPrivateMessageEndpoints()
        {
            var read = GrantName.Of(GrantArea.PrivateMessage, GrantAction.Read);
            var write = GrantName.Of(GrantArea.PrivateMessage, GrantAction.Write);
            var delete = GrantName.Of(GrantArea.PrivateMessage, GrantAction.Delete);
            var group = application.MapGroup("/private-messages").RequireAuthorization(read);
            group.MapGet("", PrivateMessageEndpoints.List).Produces(200).Produces(400).Produces(401).Produces(403);
            group.MapGet("/{id:guid}", PrivateMessageEndpoints.Get).Produces(200).Produces(401).Produces(403).Produces(404);
            group.MapPost("", PrivateMessageEndpoints.Create).RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<PrivateMessageEndpoints.CreateRequest>>()
                .Produces(201).Produces(400).Produces(401).Produces(403);
            group.MapPost("/{id:guid}/reply", PrivateMessageEndpoints.Reply).RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<PrivateMessageEndpoints.ReplyRequest>>()
                .Produces(200).Produces(400).Produces(401).Produces(403).Produces(404);
            group.MapPost("/{id:guid}/participants", PrivateMessageEndpoints.Invite).RequireAuthorization(write)
                .AddEndpointFilter<DataAnnotationValidationFilter<PrivateMessageEndpoints.InviteRequest>>()
                .WithSummary("Invite additional visible or hidden recipients to a private group.")
                .Produces(200).Produces(400).Produces(401).Produces(403).Produces(404);
            application.MapGet("/private-messages/recipients", PrivateMessageEndpoints.Recipients)
                .RequireAuthorization().Produces(200).Produces(401).Produces(403);
            group.MapGet("/blocks", PrivateMessageEndpoints.Blocks).Produces(200).Produces(401).Produces(403);
            group.MapPost("/blocks/{userId:guid}", PrivateMessageEndpoints.Block).RequireAuthorization(write)
                .Produces(200).Produces(400).Produces(401).Produces(403).Produces(404);
            group.MapPost("/blocks/{userId:guid}/delete", PrivateMessageEndpoints.Block).RequireAuthorization(write)
                .Produces(200).Produces(400).Produces(401).Produces(403).Produces(404);
            foreach (var action in new[] { "read", "delete", "restore", "join", "leave" })
            {
                var endpoint = group.MapPost("/{id:guid}/" + action, PrivateMessageEndpoints.Action)
                    .Produces(200).Produces(400).Produces(401).Produces(403).Produces(404);
                if (action == "delete")
                {
                    endpoint.RequireAuthorization(delete);
                }
                else if (action is "join" or "leave")
                {
                    endpoint.RequireAuthorization(write);
                }
            }
        }
    }
}
