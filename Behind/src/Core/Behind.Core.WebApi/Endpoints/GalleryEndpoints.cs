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

public sealed class GalleryEndpoints
{
    public const int DefaultPageSize = ManagedListExtensions.DefaultPageSize;
    public const int MaxPageSize = ManagedListExtensions.MaxPageSize;

    public sealed record SaveGalleryItemRequest(
        [property: Required] Guid ImageId,
        [property: Required] Guid ThumbnailId,
        [property: MaxLength(GalleryItem.MaxDescriptionLength)] string? Description);

    public sealed record SaveGalleryRequest(
        [property: Required, MaxLength(Gallery.MaxTitleLength)] string Title,
        [property: MaxLength(Gallery.MaxDescriptionLength)] string? Description,
        Guid? ThumbnailImageId,
        IReadOnlyList<SaveGalleryItemRequest>? Items,
        bool Publish = false);

    [IgnoreETag]
    public static async Task<IResult> List(
        HttpContext httpContext,
        IGalleryQueries queries,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? search = null)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var galleries = await queries.List(
            Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
            search.ToSearchTerm(ManagedListExtensions.MaxSearchLength),
            httpContext.RequestAborted);

        return Results.Json(new PublicGalleryListResponse(galleries.ToPageResponse(item => item.ToItem())));
    }

    [IgnoreETag]
    public static Task<IResult> Manage(
        HttpContext httpContext,
        IGalleryQueries queries,
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
                var galleries = await queries.ListManaged(
                    Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                    query.Search,
                    query.Letter,
                    query.State,
                    httpContext.RequestAborted);

                var letters = await queries.GetLetters(query.Search, query.State, httpContext.RequestAborted);

                return Results.Json(new GalleryManageResponse(
                    galleries.ToPageResponse(item => item.ToItem(query.State)),
                    letters));
            });

    public static async Task<IResult> Get(Guid id, HttpContext httpContext, IGalleryQueries queries)
    {
        httpContext.Response.Headers.CacheControl = NewsEndpoints.NoCache;

        var gallery = await queries.Find(id, httpContext.RequestAborted);

        return gallery.Match(() => Results.NotFound(), found => Results.Json(found.ToResponse()));
    }

    [IgnoreETag]
    public static async Task<IResult> GetHistory(Guid id, HttpContext httpContext, IGalleryQueries queries)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var history = await queries.FindHistory(id, httpContext.RequestAborted);

        return history.Match(() => Results.NotFound(), found => Results.Json(found.ToResponse()));
    }

    [IgnoreETag]
    public static async Task<IResult> GetVersion(
        Guid id,
        long version,
        HttpContext httpContext,
        IGalleryQueries queries)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var gallery = await queries.FindVersion(new VersionedKey<Guid>(id, version), httpContext.RequestAborted);

        return gallery.Match(() => Results.NotFound(), found => Results.Json(found.ToResponse()));
    }

    [IgnoreETag]
    public static Task<IResult> Create(
        SaveGalleryRequest request,
        HttpContext httpContext,
        ISessionQueries sessionQueries,
        IGalleryCommands commands,
        GalleryThumbnails thumbnails) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft =>
            {
                var current = await httpContext.FindCurrentSession(sessionQueries);

                return await current.Match(
                    () => Task.FromResult<IResult>(Results.Unauthorized()),
                    async session =>
                    {
                        var completed = await thumbnails.Complete(draft, httpContext.RequestAborted);

                        return (await commands.Create(
                                session.UserId,
                                completed,
                                request.Publish,
                                httpContext.RequestAborted))
                            .ToResult();
                    });
            });

    [IgnoreETag]
    public static Task<IResult> SaveVersion(
        Guid id,
        SaveGalleryRequest request,
        HttpContext httpContext,
        IGalleryCommands commands,
        GalleryThumbnails thumbnails) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft =>
            {
                var completed = await thumbnails.Complete(draft, httpContext.RequestAborted);

                return (await commands.SaveVersion(id, completed, request.Publish, httpContext.RequestAborted))
                    .ToResult();
            });

    [IgnoreETag]
    public static async Task<IResult> Activate(
        Guid id,
        long version,
        HttpContext httpContext,
        IGalleryCommands commands) =>
        (await commands.Activate(new VersionedKey<Guid>(id, version), httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid id, HttpContext httpContext, IGalleryCommands commands) =>
        await commands.Delete(id, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, IGalleryCommands commands) =>
        await commands.Restore(id, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid id, HttpContext httpContext, IGalleryCommands commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();
}
