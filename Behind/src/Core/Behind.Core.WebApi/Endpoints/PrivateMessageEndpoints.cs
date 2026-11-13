using System.ComponentModel.DataAnnotations;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Behind.Core.WebApi.Endpoints;

public static class PrivateMessageEndpoints
{
    public sealed record CreateRequest(
        [property: Required, MaxLength(200)] string Subject,
        [property: Required, MaxLength(100_000)] string Content,
        [property: MaxLength(100)] Guid[]? UserIds,
        [property: MaxLength(100)] Guid[]? RoleIds,
        [property: MaxLength(100)] Guid[]? BccUserIds,
        [property: MaxLength(100)] Guid[]? BccRoleIds,
        bool IsGroup = true, bool IsSystem = false);

    public sealed record ReplyRequest(
        [property: Required, MaxLength(100_000)] string Content, Guid? ReplyToId);

    public sealed record InviteRequest(
        [property: MaxLength(100)] Guid[]? UserIds,
        [property: MaxLength(100)] Guid[]? RoleIds,
        [property: MaxLength(100)] Guid[]? BccUserIds,
        [property: MaxLength(100)] Guid[]? BccRoleIds);

    private sealed record Viewer(Guid Id, bool SystemModerator, bool ReportModerator);

    [IgnoreETag]
    public static Task<IResult> List(
        HttpContext http, IModuleIdentity identity, IAuthorizationService authorization,
        PrivateMessageStore store, [FromQuery] string folder = "inbox",
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        WithViewer(http, identity, authorization, async viewer =>
        {
            if (folder is not ("inbox" or "read" or "sent" or "deleted" or "left")
                || page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
            {
                return Results.BadRequest(new { error = "invalidMailboxQuery" });
            }

            var result = await store.List(viewer.Id, folder, page, pageSize, viewer.SystemModerator,
                viewer.ReportModerator, http.RequestAborted);
            return Results.Ok(new { result.Items, result.Total, page = result.PageNumber, result.PageSize, result.PageCount });
        });

    [IgnoreETag]
    public static Task<IResult> Get(
        Guid id, HttpContext http, IModuleIdentity identity, IAuthorizationService authorization,
        PrivateMessageStore store) =>
        WithViewer(http, identity, authorization, async viewer =>
        {
            var conversation = await store.Find(viewer.Id, id, viewer.SystemModerator, viewer.ReportModerator, http.RequestAborted);
            return conversation is null ? Results.NotFound() : Results.Ok(conversation);
        });

    [IgnoreETag]
    public static Task<IResult> Create(
        CreateRequest request, HttpContext http, IModuleIdentity identity,
        IAuthorizationService authorization, PrivateMessageStore store, ILogger<PrivateMessageStore> logger) =>
        WithViewer(http, identity, authorization, async viewer =>
        {
            if ((request.IsSystem || request.RoleIds?.Length > 0 || request.BccRoleIds?.Length > 0)
                && !viewer.SystemModerator)
            {
                return Results.Forbid();
            }

            var result = await store.Create(viewer.Id, request.Subject, request.Content,
                request.UserIds ?? [], request.RoleIds ?? [], request.BccUserIds ?? [], request.BccRoleIds ?? [],
                request.IsGroup, request.IsSystem, viewer.SystemModerator, cancellationToken: http.RequestAborted);
            return result.Succeeded
                ? Results.Created($"/private-messages/{result.Id}", new { id = result.Id })
                : Failure(result, http, logger);
        });

    [IgnoreETag]
    public static Task<IResult> Reply(
        Guid id, ReplyRequest request, HttpContext http, IModuleIdentity identity,
        IAuthorizationService authorization, PrivateMessageStore store, ILogger<PrivateMessageStore> logger) =>
        WithViewer(http, identity, authorization, async viewer =>
            Outcome(await store.Reply(viewer.Id, id, request.Content, request.ReplyToId,
                viewer.SystemModerator, viewer.ReportModerator, http.RequestAborted), http, logger));

    [IgnoreETag]
    public static Task<IResult> Invite(
        Guid id, InviteRequest request, HttpContext http, IModuleIdentity identity,
        IAuthorizationService authorization, PrivateMessageStore store, ILogger<PrivateMessageStore> logger) =>
        WithViewer(http, identity, authorization, async viewer =>
            Outcome(await store.Invite(viewer.Id, id, request.UserIds ?? [], request.RoleIds ?? [],
                request.BccUserIds ?? [], request.BccRoleIds ?? [], http.RequestAborted, viewer.SystemModerator), http, logger));

    [IgnoreETag]
    public static Task<IResult> Action(
        Guid id, HttpContext http, IModuleIdentity identity, IAuthorizationService authorization,
        PrivateMessageStore store, ILogger<PrivateMessageStore> logger) =>
        WithViewer(http, identity, authorization, async viewer =>
        {
            var action = http.Request.Path.Value!.Split('/')[^1];
            var result = action switch
            {
                "join" => await store.Join(viewer.Id, id, http.RequestAborted),
                "leave" => await store.Leave(viewer.Id, id, http.RequestAborted),
                "read" or "delete" or "restore" => await store.SetMailbox(viewer.Id, id, action,
                    http.RequestAborted, viewer.SystemModerator, viewer.ReportModerator),
                _ => throw new InvalidOperationException("Unmapped private messaging action.")
            };
            return Outcome(result, http, logger);
        });

    [IgnoreETag]
    public static async Task<IResult> Recipients(
        HttpContext http, IModuleIdentity identity, IAuthorizationService authorization,
        PrivateMessageStore store, [FromQuery] string? search = null, [FromQuery] bool reportTargets = false)
    {
        http.Response.Headers.CacheControl = "no-store";
        var viewer = await identity.UserId(http);
        if (viewer is null)
        {
            return Results.Unauthorized();
        }

        if (!await authorization.HoldsGrantAsync(http.User, GrantName.Of(GrantArea.PrivateMessage, GrantAction.Read))
            && !await authorization.HoldsGrantAsync(http.User, GrantName.Of(GrantArea.Report, GrantAction.Write)))
        {
            return Results.Forbid();
        }

        if (search?.Length > 200)
        {
            return Results.BadRequest(new { error = "invalidRecipientSearch" });
        }

        var found = await store.Recipients(search, http.RequestAborted, reportTargets, viewer);
        var canSendRoles = await authorization.HoldsGrantAsync(http.User,
            GrantName.Of(GrantArea.PrivateMessageModeration, GrantAction.Manage));
        return Results.Ok(new
        {
            found.Users,
            roles = found.Roles.Where(_ => canSendRoles)
                .Select(role => new { role.Id, name = role.DisplayName, role.DisplayName, role.IsRole })
        });
    }

    [IgnoreETag]
    public static Task<IResult> Blocks(
        HttpContext http, IModuleIdentity identity, IAuthorizationService authorization, PrivateMessageStore store,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        WithViewer(http, identity, authorization, async viewer =>
        {
            if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
            {
                return Results.BadRequest(new { error = "invalidMailboxQuery" });
            }

            var blocks = await store.Blocks(viewer.Id, http.RequestAborted);
            var items = blocks.OrderBy(user => user.DisplayName, StringComparer.Ordinal)
                .ThenBy(user => user.Id).Skip((page - 1) * pageSize).Take(pageSize);
            return Results.Ok(new { items, total = blocks.Count, page, pageSize,
                pageCount = (int)Math.Ceiling((double)blocks.Count / pageSize) });
        });

    [IgnoreETag]
    public static Task<IResult> Block(
        Guid userId, HttpContext http, IModuleIdentity identity, IAuthorizationService authorization,
        PrivateMessageStore store, ILogger<PrivateMessageStore> logger) =>
        WithViewer(http, identity, authorization, async viewer =>
            Outcome(await store.SetBlock(viewer.Id, userId,
                !http.Request.Path.Value!.EndsWith("/delete", StringComparison.Ordinal), http.RequestAborted), http, logger));

    private static async Task<IResult> WithViewer(
        HttpContext http, IModuleIdentity identity, IAuthorizationService authorization, Func<Viewer, Task<IResult>> action)
    {
        http.Response.Headers.CacheControl = "no-store";
        var id = await identity.UserId(http);
        if (id is null)
        {
            return Results.Unauthorized();
        }

        if (!await authorization.HoldsGrantAsync(http.User, GrantName.Of(GrantArea.PrivateMessage, GrantAction.Read)))
        {
            return Results.Forbid();
        }

        var systemModerator = await authorization.HoldsGrantAsync(http.User,
            GrantName.Of(GrantArea.PrivateMessageModeration, GrantAction.Manage));
        var reportModerator = await authorization.HoldsGrantAsync(http.User, GrantName.Of(GrantArea.Report, GrantAction.Manage));
        return await action(new(id.Value, systemModerator, reportModerator));
    }

    private static IResult Outcome(PrivateMessageStore.Result result, HttpContext http, ILogger logger) =>
        result.Succeeded ? Results.Ok(new { id = result.Id }) : Failure(result, http, logger);

    private static IResult Failure(PrivateMessageStore.Result result, HttpContext http, ILogger logger)
    {
        logger.LogWarning("Rejected private messaging {Method} {Path}: {Error}",
            http.Request.Method, http.Request.Path.Value, result.Error);
        return result.Error switch
        {
            "not-found" => Results.NotFound(),
            "forbidden" => Results.Forbid(),
            "blocked" => Results.Json(new { error = "blocked" }, statusCode: StatusCodes.Status403Forbidden),
            _ => Results.BadRequest(new { error = result.Error })
        };
    }
}
