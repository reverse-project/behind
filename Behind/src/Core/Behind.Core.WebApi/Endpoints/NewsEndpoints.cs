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

public sealed class NewsEndpoints
{
    public const int DefaultPageSize = ManagedListExtensions.DefaultPageSize;
    public const int MaxPageSize = ManagedListExtensions.MaxPageSize;
    public const string DeletedRecordsGrant = ManagedListExtensions.DeletedRecordsGrant;
    public const string NoStore = ManagedListExtensions.NoStore;
    public const string NoCache = "no-cache";

    public sealed record SaveNewsRequest(
        [property: Required, MaxLength(News.MaxTitleLength)] string Title,
        [property: MaxLength(News.MaxSummaryLength)] string? Summary,
        [property: MaxLength(News.MaxContentLength)] string? Content,
        Guid? CategoryId,
        bool Highlighted = false,
        DateTimeOffset? StartAt = null,
        DateTimeOffset? EndAt = null,
        bool Publish = false);

    [IgnoreETag]
    public static async Task<IResult> List(
        HttpContext httpContext,
        INewsQueries queries,
        INewsCategoryQueries categories,
        TimeProvider timeProvider,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] Guid? category = null)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        var news = await queries.List(
            Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
            category.ToOption(),
            timeProvider.GetUtcNow(),
            httpContext.RequestAborted);

        var active = await categories.ListActive(httpContext.RequestAborted);

        return Results.Json(new PublicNewsListResponse(
            news.ToPageResponse(item => item.ToItem()),
            active.Select(item => item.ToItem(DeletionState.Active)).ToList()));
    }

    [IgnoreETag]
    public static Task<IResult> Manage(
        HttpContext httpContext,
        INewsQueries queries,
        IAuthorizationService authorization,
        TimeProvider timeProvider,
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
                var news = await queries.ListManaged(
                    Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                    query.Search,
                    query.Letter,
                    query.State,
                    httpContext.RequestAborted);

                var letters = await queries.GetLetters(query.Search, query.State, httpContext.RequestAborted);
                var now = timeProvider.GetUtcNow();

                return Results.Json(new NewsManageResponse(
                    news.ToPageResponse(item => item.ToItem(query.State, now)),
                    letters));
            });

    public static async Task<IResult> Get(
        Guid id,
        HttpContext httpContext,
        INewsQueries queries,
        TimeProvider timeProvider)
    {
        httpContext.Response.Headers.CacheControl = NoCache;

        var news = await queries.Find(id, timeProvider.GetUtcNow(), httpContext.RequestAborted);

        return news.Match(() => Results.NotFound(), found => Results.Json(found.ToResponse()));
    }

    [IgnoreETag]
    public static async Task<IResult> GetHistory(Guid id, HttpContext httpContext, INewsQueries queries)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        var history = await queries.FindHistory(id, httpContext.RequestAborted);

        return history.Match(() => Results.NotFound(), found => Results.Json(found.ToResponse()));
    }

    [IgnoreETag]
    public static async Task<IResult> GetVersion(Guid id, long version, HttpContext httpContext, INewsQueries queries)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        var news = await queries.FindVersion(new VersionedKey<Guid>(id, version), httpContext.RequestAborted);

        return news.Match(() => Results.NotFound(), found => Results.Json(found.ToResponse()));
    }

    [IgnoreETag]
    public static Task<IResult> Create(
        SaveNewsRequest request,
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        INewsCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft =>
            {
                var current = await httpContext.FindCurrentSession(sessionQueries);

                return await current.Match(
                    () => Task.FromResult<IResult>(Results.Unauthorized()),
                    async session =>
                        (await commands.Create(session.UserId, draft, request.Publish, httpContext.RequestAborted))
                        .ToResult());
            });

    [IgnoreETag]
    public static Task<IResult> SaveVersion(
        Guid id,
        SaveNewsRequest request,
        HttpContext httpContext,
        INewsCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft =>
                (await commands.SaveVersion(id, draft, request.Publish, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static async Task<IResult> Activate(
        Guid id,
        long version,
        HttpContext httpContext,
        INewsCommands commands) =>
        (await commands.Activate(new VersionedKey<Guid>(id, version), httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid id, HttpContext httpContext, INewsCommands commands) =>
        await commands.Delete(id, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, INewsCommands commands) =>
        await commands.Restore(id, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid id, HttpContext httpContext, INewsCommands commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();
}
