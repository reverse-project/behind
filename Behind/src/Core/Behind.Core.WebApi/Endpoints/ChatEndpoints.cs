using Behind.Common;
using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Core.WebApi.Endpoints;

public static class ChatEndpoints
{
    public static readonly string ReadGrant = GrantName.Of(GrantArea.Chat, GrantAction.Read);
    public static readonly string WriteGrant = GrantName.Of(GrantArea.Chat, GrantAction.Write);
    public static readonly string DeleteGrant = GrantName.Of(GrantArea.Chat, GrantAction.Delete);
    public static readonly string ModerationGrant = GrantName.Of(GrantArea.Chat, GrantAction.Manage);

    public sealed record ChatRequest(string? Text, Guid? ReplyToId = null, bool IsAnonymous = false);
    public sealed record EditRequest(string? Text, long Version, bool? IsAnonymous = null);
    public sealed record Reply(Guid Id, string Author, string Text, Guid? AuthorId, bool IsAnonymous = false);
    public sealed record Message(
        Guid Id, long Version, string Author, string? ProfilePictureUrl, string Text,
        DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, DateTimeOffset EditableUntil,
        bool CanEdit, bool CanDelete, Guid? ReplyToId, Reply? Reply, Guid? AuthorId, bool IsAnonymous = false);
    public sealed record Page(IReadOnlyList<Message> Items, Guid? NextCursor, bool CanWrite);

    [IgnoreETag]
    public static async Task<IResult> List(
        HttpContext http, IChatStore store, IModuleIdentity identity, IModuleUsers users,
        IAuthorizationService authorization, TimeProvider timeProvider, CancellationToken cancellationToken,
        [FromQuery] Guid? before = null, [FromQuery] int limit = 20)
    {
        http.Response.Headers.CacheControl = "no-store";

        if (!await CanRead(http, identity, authorization))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (limit is < 1 or > 50)
        {
            return Results.BadRequest();
        }

        var page = await store.List(before, limit, cancellationToken);

        if (page is null)
        {
            return Results.BadRequest();
        }

        return Results.Ok(await Response(page, http, store, identity, users, authorization, timeProvider, cancellationToken));
    }

    [IgnoreETag]
    public static async Task<IResult> Get(
        Guid id, HttpContext http, IChatStore store, IModuleIdentity identity, IModuleUsers users,
        IAuthorizationService authorization, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        http.Response.Headers.CacheControl = "no-store";

        if (!await CanRead(http, identity, authorization))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var messages = await store.Find([id], cancellationToken);

        if (messages.Count == 0)
        {
            return Results.NotFound();
        }

        var page = await Response(new(messages, null), http, store, identity, users, authorization, timeProvider, cancellationToken);
        return Results.Ok(page.Items[0]);
    }

    private static async Task<Page> Response(
        IChatStore.Page page, HttpContext http, IChatStore store, IModuleIdentity identity, IModuleUsers users,
        IAuthorizationService authorization, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var viewer = await identity.UserId(http);
        var moderator = viewer is not null && await authorization.HoldsGrantAsync(http.User, ModerationGrant);
        var canDeleteOwn = viewer is not null && await authorization.HoldsGrantAsync(http.User, DeleteGrant);
        var canWrite = viewer is not null && await authorization.HoldsGrantAsync(http.User, WriteGrant);
        var parents = await store.Find(page.Items.Where(message => message.ReplyToId.HasValue)
            .Select(message => message.ReplyToId!.Value).Distinct().ToArray(), cancellationToken);
        var profiles = (await users.Find(page.Items.Concat(parents).Where(message => !message.IsAnonymous || moderator)
            .Select(message => message.AuthorId).Distinct().ToArray(),
            cancellationToken)).ToDictionary(profile => profile.Id);
        var replies = parents.ToDictionary(parent => parent.Id, parent =>
            new Reply(parent.Id, parent.IsAnonymous && !moderator ? ""
                : profiles.GetValueOrDefault(parent.AuthorId)?.DisplayName ?? parent.AuthorId.ToUserLabel(null),
                parent.Text, parent.IsAnonymous && !moderator ? null : parent.AuthorId, parent.IsAnonymous));
        var now = timeProvider.GetUtcNow();

        return new Page(page.Items.Select(message =>
        {
            var hidden = message.IsAnonymous && !moderator;
            var profile = hidden ? null : profiles.GetValueOrDefault(message.AuthorId);
            return new Message(
                message.Id, message.Version, hidden ? "" : profile?.DisplayName ?? message.AuthorId.ToUserLabel(null),
                profile?.PictureId is { } picture ? $"/profile/images/{picture}" : null,
                message.Text, message.CreatedAt, message.UpdatedAt, message.CreatedAt + ChatMessage.EditWindow,
                canWrite && viewer == message.AuthorId && now < message.CreatedAt + ChatMessage.EditWindow,
                moderator || canDeleteOwn && viewer == message.AuthorId, message.ReplyToId,
                message.ReplyToId is { } parent ? replies.GetValueOrDefault(parent) : null,
                hidden ? null : message.AuthorId, message.IsAnonymous);
        }).ToArray(), page.NextCursor, canWrite);
    }

    [IgnoreETag]
    public static async Task<IResult> Add(
        ChatRequest request, HttpContext http, IChatStore store, IModuleIdentity identity, IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var viewer = await identity.UserId(http);

        if (viewer is null)
        {
            return Results.Unauthorized();
        }

        if (!await authorization.HoldsGrantAsync(http.User, ReadGrant)
            || !await authorization.HoldsGrantAsync(http.User, WriteGrant))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var text = request.Text?.Trim() ?? "";

        if (text.Length is 0 or > ChatMessage.MaxTextLength)
        {
            return Results.BadRequest();
        }

        var result = await store.Add(viewer.Value, text, request.ReplyToId, cancellationToken, request.IsAnonymous);
        return result.Status == IChatStore.SaveStatus.Saved
            ? Results.Created($"/widgets/chat/{result.Id}", new { result.Id })
            : Failure(result.Status);
    }

    [IgnoreETag]
    public static async Task<IResult> Update(
        Guid id, EditRequest request, HttpContext http, IChatStore store, IModuleIdentity identity, IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var viewer = await identity.UserId(http);

        if (viewer is null)
        {
            return Results.Unauthorized();
        }

        if (!await authorization.HoldsGrantAsync(http.User, ReadGrant)
            || !await authorization.HoldsGrantAsync(http.User, WriteGrant))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var text = request.Text?.Trim() ?? "";

        if (text.Length is 0 or > ChatMessage.MaxTextLength || request.Version < 1)
        {
            return Results.BadRequest();
        }

        var result = await store.Update(id, request.Version, viewer.Value, text, cancellationToken, request.IsAnonymous);
        return result.Status == IChatStore.SaveStatus.Saved ? Results.NoContent() : Failure(result.Status);
    }

    [IgnoreETag]
    public static async Task<IResult> Delete(
        Guid id, HttpContext http, IChatStore store, IModuleIdentity identity,
        IAuthorizationService authorization, CancellationToken cancellationToken)
    {
        var viewer = await identity.UserId(http);

        if (viewer is null)
        {
            return Results.Unauthorized();
        }

        if (!await authorization.HoldsGrantAsync(http.User, ReadGrant))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var moderator = await authorization.HoldsGrantAsync(http.User, ModerationGrant);
        var deleteOwn = await authorization.HoldsGrantAsync(http.User, DeleteGrant);

        if (!moderator && !deleteOwn)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var messages = await store.Find([id], cancellationToken);

        if (messages.Count == 0)
        {
            return Results.NotFound();
        }

        if (!moderator && messages[0].AuthorId != viewer)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return await store.Delete(id, cancellationToken) ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<bool> CanRead(
        HttpContext http, IModuleIdentity identity, IAuthorizationService authorization) =>
        await identity.UserId(http) is not null
            ? await authorization.HoldsGrantAsync(http.User, ReadGrant)
            : http.User.Identity?.IsAuthenticated != true;

    private static IResult Failure(IChatStore.SaveStatus status) => status switch
    {
        IChatStore.SaveStatus.NotFound => Results.NotFound(),
        IChatStore.SaveStatus.Forbidden or IChatStore.SaveStatus.Expired => Results.StatusCode(StatusCodes.Status403Forbidden),
        IChatStore.SaveStatus.InvalidReply => Results.BadRequest(),
        IChatStore.SaveStatus.Conflict => Results.Conflict(),
        _ => throw new InvalidOperationException($"Unexpected chat save status: {status}")
    };
}
