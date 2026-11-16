using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class CarouselEndpoints
{
    public const string NoCache = SiteEndpoints.NoCache;

    public sealed record SaveCarouselSlideRequest(
        [property: Required] Guid ImageId,
        [property: MaxLength(CarouselSlide.MaxCaptionLength)] string? Caption,
        [property: MaxLength(CarouselSlide.MaxLinkUrlLength)] string? LinkUrl,
        Guid? Id = null);

    public sealed record SaveCarouselRequest(
        int IntervalSeconds,
        [property: Required] IReadOnlyList<SaveCarouselSlideRequest>? Slides);

    [IgnoreETag]
    public static IResult GetSettings(HttpContext httpContext, IOptionsSnapshot<CarouselsConfiguration> configuration)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        return Results.Json(configuration.Value.ToResponse());
    }

    [IgnoreETag]
    public static async Task<IResult> GetPublic(Guid siteId, HttpContext httpContext, ICarouselQueries queries)
    {
        httpContext.Response.Headers.CacheControl = NoCache;

        var carousel = await queries.FindLive(siteId, httpContext.RequestAborted);

        return carousel.Match(() => Results.NotFound(), found => Results.Json(found.ToResponse()));
    }

    [IgnoreETag]
    public static Task<IResult> GetManaged(
        Guid siteId,
        HttpContext httpContext,
        ICarouselQueries queries,
        IAuthorizationService authorization,
        [FromQuery] bool deleted = false) =>
        httpContext.WithDeletionState(
            authorization,
            deleted,
            async state =>
            {
                var carousel = await queries.FindManaged(siteId, state, httpContext.RequestAborted);

                return carousel.Match(() => Results.NotFound(), found => Results.Json(found.ToResponse()));
            });

    [IgnoreETag]
    public static Task<IResult> Save(
        Guid siteId,
        SaveCarouselRequest request,
        HttpContext httpContext,
        ICarouselCommands commands,
        IOptionsSnapshot<CarouselsConfiguration> configuration) =>
        request.ToDraft(configuration.Value).Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Save(siteId, draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid siteId, HttpContext httpContext, ICarouselCommands commands) =>
        await commands.Delete(siteId, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid siteId, HttpContext httpContext, ICarouselCommands commands) =>
        await commands.Restore(siteId, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid siteId, HttpContext httpContext, ICarouselCommands commands) =>
        (await commands.HardDelete(siteId, httpContext.RequestAborted)).ToResult();
}
