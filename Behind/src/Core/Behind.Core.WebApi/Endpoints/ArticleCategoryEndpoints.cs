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

public sealed class ArticleCategoryEndpoints
{
    public const int DefaultPageSize = ManagedListExtensions.DefaultPageSize;
    public const int MaxPageSize = ManagedListExtensions.MaxPageSize;

    public sealed record SaveArticleCategoryRequest(
        [property: Required, MaxLength(ArticleCategory.MaxNameLength)] string Name,
        Guid? HeaderImageId);

    [IgnoreETag]
    public static async Task<IResult> ListActive(HttpContext httpContext, IArticleCategoryQueries queries)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var categories = await queries.ListActive(httpContext.RequestAborted);

        return Results.Json(categories.Select(category => category.ToItem(DeletionState.Active)).ToList());
    }

    [IgnoreETag]
    public static Task<IResult> Manage(
        HttpContext httpContext,
        IArticleCategoryQueries queries,
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
                var categories = await queries.List(
                    Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                    query.Search,
                    query.Letter,
                    query.State,
                    httpContext.RequestAborted);

                var letters = await queries.GetLetters(query.Search, query.State, httpContext.RequestAborted);

                return Results.Json(new ArticleCategoryManageResponse(
                    categories.ToPageResponse(category => category.ToItem(query.State)),
                    letters));
            });

    [IgnoreETag]
    public static Task<IResult> Create(
        SaveArticleCategoryRequest request,
        HttpContext httpContext,
        IArticleCategoryCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Create(draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static Task<IResult> Update(
        Guid id,
        SaveArticleCategoryRequest request,
        HttpContext httpContext,
        IArticleCategoryCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Update(id, draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid id, HttpContext httpContext, IArticleCategoryCommands commands) =>
        await commands.Delete(id, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, IArticleCategoryCommands commands)
    {
        var result = await commands.Restore(id, httpContext.RequestAborted);

        return result is ArticleCategorySaveResult.Saved ? Results.NoContent() : result.ToResult();
    }

    [IgnoreETag]
    public static async Task<IResult> HardDelete(
        Guid id,
        HttpContext httpContext,
        IArticleCategoryCommands commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();
}
