using Behind.Common.WebApi.Middleware;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Mcp;

public static class McpRoutesExtensions
{
    extension(WebApplication application)
    {
        public WebApplication MapMcp()
        {
            application.MapPost("/mcp", (HttpContext context, IMcpServer server, CancellationToken cancellationToken) =>
                    server.Handle(context, cancellationToken))
                .AllowAnonymous()
                .WithMetadata(new IgnoreETagAttribute())
                .WithSummary("Execute an authenticated MCP JSON-RPC message over stateless Streamable HTTP");
            application.MapMethods("/mcp", ["GET", "DELETE"], (HttpContext context) =>
                {
                    context.Response.Headers.Allow = "POST";
                    return TypedResults.StatusCode(StatusCodes.Status405MethodNotAllowed);
                })
                .AllowAnonymous()
                .WithMetadata(new IgnoreETagAttribute())
                .WithSummary("MCP server streams and transport sessions are not supported");

            var group = application.MapGroup("/service-users")
                .RequireAuthorization(GrantName.Of(GrantArea.Configuration, GrantAction.Read))
                .WithMetadata(new IgnoreETagAttribute());
            group.MapGet("", ServiceUserEndpoints.List)
                .WithSummary("List service users without exposing credential hashes");
            group.MapGet("/{id:guid}", ServiceUserEndpoints.Get)
                .WithSummary("Get a service identity without exposing credential hashes");
            var manage = GrantName.Of(GrantArea.Configuration, GrantAction.Manage);
            var users = GrantName.Of(GrantArea.User, GrantAction.Manage);
            group.MapPost("", ServiceUserEndpoints.Create)
                .RequireAuthorization(manage, users)
                .Produces<ServiceUserCredentialResponse>(StatusCodes.Status201Created)
                .WithSummary("Create a service user and reveal its bearer key once");
            group.MapPut("/{id:guid}", ServiceUserEndpoints.Update)
                .RequireAuthorization(manage, users)
                .WithSummary("Enable or disable the observed service user version");
            group.MapPost("/{id:guid}/rotate", ServiceUserEndpoints.Rotate)
                .RequireAuthorization(manage, users)
                .WithSummary("Rotate a service user's bearer key, revoking the previous key");
            group.MapGet("/{id:guid}/access", ServiceUserEndpoints.GetAccess)
                .WithSummary("Read a service user's independent roles, grants and restrictions");
            group.MapPut("/{id:guid}/access", ServiceUserEndpoints.SetAccess)
                .RequireAuthorization(manage, users)
                .WithSummary("Manage a service user's own access without inheriting the creator's permissions");
            group.MapPost("/{id:guid}/delete", ServiceUserEndpoints.Delete)
                .RequireAuthorization(manage, GrantName.Of(GrantArea.User, GrantAction.Delete))
                .WithSummary("Soft delete a service user and revoke access");
            group.MapPost("/{id:guid}/restore", ServiceUserEndpoints.Restore)
                .RequireAuthorization(manage)
                .WithSummary("Restore a deleted service user as a developer");
            group.MapPost("/{id:guid}/hard/delete", ServiceUserEndpoints.HardDelete)
                .RequireAuthorization(manage)
                .WithSummary("Permanently delete a service user as a developer");
            return application;
        }
    }
}
