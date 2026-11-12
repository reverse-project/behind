using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Core.WebApi.Endpoints;

public sealed class MessageEndpoints
{
    public const int DefaultPageSize = ManagedListExtensions.DefaultPageSize;
    public const int MaxPageSize = ManagedListExtensions.MaxPageSize;

    public sealed record SaveMessageRequest(
        [property: Required, MaxLength(Message.MaxNameLength * 2)] string Name,
        [property: MaxLength(Message.MaxTitleLength * 2)] string? Title,
        [property: MaxLength(Message.MaxContentLength)] string? Content,
        [property: Required, MaxLength(Message.MaxLevelLength)] string Level,
        DateTimeOffset? StartsAt,
        DateTimeOffset? EndsAt);

    [IgnoreETag]
    public static Task<IResult> Manage(
        HttpContext httpContext,
        IMessageQueries queries,
        IAuthorizationService authorization,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? search = null,
        [FromQuery] string? letter = null,
        [FromQuery] bool deleted = false) =>
        httpContext.WithManagedList(
            authorization,
            search,
            letter,
            deleted,
            async query =>
            {
                var messages = await queries.List(
                    Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                    query.Search,
                    query.Letter,
                    query.State,
                    httpContext.RequestAborted);

                var letters = await queries.GetLetters(query.Search, query.State, httpContext.RequestAborted);

                return Results.Json(new MessageManageResponse(
                    messages.ToPageResponse(message => message.ToItem(query.State)),
                    letters));
            });

    [IgnoreETag]
    public static async Task<IResult> Get(
        Guid id,
        HttpContext httpContext,
        IMessageQueries queries,
        IAuthorizationService authorization)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var active = await queries.Find(id, DeletionState.Active, httpContext.RequestAborted);

        return await active.Match(
            () => FindDeleted(id, httpContext, queries, authorization),
            message => Task.FromResult<IResult>(Results.Json(message.ToResponse(DeletionState.Active))));
    }

    [IgnoreETag]
    public static async Task<IResult> Live(
        Guid id,
        HttpContext httpContext,
        IMessageQueries queries,
        TimeProvider timeProvider)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var message = await queries.FindLive(id, timeProvider.GetUtcNow(), httpContext.RequestAborted);

        return message.Match(
            () => Results.NotFound(),
            found => Results.Json(found.ToWidget()));
    }

    [IgnoreETag]
    public static Task<IResult> Create(
        SaveMessageRequest request,
        HttpContext httpContext,
        IMessageCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Create(draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static Task<IResult> Update(
        Guid id,
        SaveMessageRequest request,
        HttpContext httpContext,
        IMessageCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Update(id, draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid id, HttpContext httpContext, IMessageCommands commands) =>
        (await commands.Delete(id, httpContext.RequestAborted)).ToNoContentResult();

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, IMessageCommands commands) =>
        (await commands.Restore(id, httpContext.RequestAborted)).ToNoContentResult();

    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid id, HttpContext httpContext, IMessageCommands commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();

    private static async Task<IResult> FindDeleted(
        Guid id,
        HttpContext httpContext,
        IMessageQueries queries,
        IAuthorizationService authorization)
    {
        if (!await authorization.HoldsGrantAsync(httpContext.User, ManagedListExtensions.DeletedRecordsGrant))
        {
            return Results.NotFound();
        }

        var deleted = await queries.Find(id, DeletionState.Deleted, httpContext.RequestAborted);

        return deleted.Match(
            () => Results.NotFound(),
            message => Results.Json(message.ToResponse(DeletionState.Deleted)));
    }
}
