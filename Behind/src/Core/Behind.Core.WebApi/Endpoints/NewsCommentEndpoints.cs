using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class NewsCommentEndpoints
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;
    public static readonly string ModerationGrant = GrantName.Of(GrantArea.News, GrantAction.Delete);
    public static readonly string EditGrant = GrantName.Of(GrantArea.News, GrantAction.Manage);

    public sealed record SaveNewsCommentRequest(
        [property: Required, MaxLength(NewsComment.MaxContentLength)] string Content,
        bool? IsAnonymous = null);

    [IgnoreETag]
    public static async Task<IResult> List(
        Guid id,
        HttpContext httpContext,
        INewsCommentQueries queries,
        ISessionQueries sessionQueries,
        IAuthorizationService authorization,
        TimeProvider timeProvider,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var viewer = (await httpContext.FindCurrentSession(sessionQueries)).Map(session => session.UserId);
        var canDeleteAny = await authorization.HoldsGrantAsync(httpContext.User, ModerationGrant);
        var canEditAny = await authorization.HoldsGrantAsync(httpContext.User, EditGrant);

        var comments = await queries.List(
            id,
            Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
            timeProvider.GetUtcNow(),
            httpContext.RequestAborted);

        return Results.Json(comments.ToPageResponse(comment => comment.ToItem(viewer, canDeleteAny, canEditAny)));
    }

    [IgnoreETag]
    public static async Task<IResult> Add(
        Guid id,
        SaveNewsCommentRequest request,
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        INewsCommentCommands commands)
    {
        var content = (request.Content ?? string.Empty).Trim();

        if (content.Length == 0)
        {
            return Results.BadRequest();
        }

        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                var added = await commands.Add(id, session.UserId, content, httpContext.RequestAborted, request.IsAnonymous ?? false);

                return added.Match(() => Results.NotFound(), commentId => Results.Json(new NewsSavedResponse(commentId, 1)));
            });
    }

    [IgnoreETag]
    public static Task<IResult> Update(
        Guid commentId,
        SaveNewsCommentRequest request,
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        INewsCommentQueries queries,
        INewsCommentCommands commands,
        IAuthorizationService authorization)
    {
        var content = (request.Content ?? string.Empty).Trim();

        return content.Length == 0
            ? Task.FromResult<IResult>(Results.BadRequest())
            : WithCommentAccess(
                commentId,
                EditGrant,
                httpContext,
                sessionQueries,
                queries,
                authorization,
                async () => await commands.Update(commentId, content, httpContext.RequestAborted, request.IsAnonymous)
                    ? Results.NoContent()
                    : Results.NotFound());
    }

    [IgnoreETag]
    public static Task<IResult> Delete(
        Guid commentId,
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        INewsCommentQueries queries,
        INewsCommentCommands commands,
        IAuthorizationService authorization) =>
        WithCommentAccess(
            commentId,
            ModerationGrant,
            httpContext,
            sessionQueries,
            queries,
            authorization,
            async () => await commands.Delete(commentId, httpContext.RequestAborted)
                ? Results.NoContent()
                : Results.NotFound());

    private static async Task<IResult> WithCommentAccess(
        Guid commentId,
        string grant,
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        INewsCommentQueries queries,
        IAuthorizationService authorization,
        Func<Task<IResult>> action)
    {
        var current = await httpContext.FindCurrentSession(sessionQueries);

        return await current.Match(
            () => Task.FromResult<IResult>(Results.Unauthorized()),
            async session =>
            {
                var author = await queries.FindAuthor(commentId, httpContext.RequestAborted);

                return await author.Match(
                    () => Task.FromResult<IResult>(Results.NotFound()),
                    async authorId =>
                    {
                        var allowed = authorId == session.UserId
                                      || await authorization.HoldsGrantAsync(httpContext.User, grant);

                        return allowed ? await action() : Results.StatusCode(StatusCodes.Status403Forbidden);
                    });
            });
    }
}
