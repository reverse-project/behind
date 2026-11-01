using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Authorization;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Site = Behind.Core.Domain.Site;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SiteEndpoints
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 200;
    public const string DeletedRecordsGrant = "RawDatabase.Manage";
    public const string NoStore = "no-store";
    public const string NoCache = "no-cache";

    public sealed record SaveSiteRequest(
        [property: Required, MaxLength(Site.MaxTitleLength)] string Title,
        [property: MaxLength(Site.MaxSlugLength * 2)] string? Slug,
        [property: MaxLength(Site.MaxContentLength)] string? Content,
        bool Publish);

    [IgnoreETag]
    public static async Task<IResult> List(
        HttpContext httpContext,
        ISiteQueries queries,
        IAuthorizationService authorization,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? search = null,
        [FromQuery] string? letter = null,
        [FromQuery] bool deleted = false)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        var letterFilter = LetterBucketExtensions.ParseLetterBucket(letter);

        if (!string.IsNullOrWhiteSpace(letter) && letterFilter == None)
        {
            return Results.BadRequest();
        }

        if (deleted && !await authorization.HoldsGrantAsync(httpContext.User, DeletedRecordsGrant))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var state = deleted ? DeletionState.Deleted : DeletionState.Active;
        var searchTerm = search.ToSearchTerm(MaxSearchLength);

        var sites = await queries.List(
            Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
            searchTerm,
            letterFilter,
            state,
            httpContext.RequestAborted);

        var letters = await queries.GetLetters(searchTerm, state, httpContext.RequestAborted);

        return Results.Json(new SiteListResponse(sites.ToPageResponse(site => site.ToItem(state)), letters));
    }

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid id, HttpContext httpContext, ISiteCommands commands) =>
        await commands.Delete(id, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, ISiteCommands commands)
    {
        var result = await commands.Restore(id, httpContext.RequestAborted);

        return result is SiteSaveResult.Saved ? Results.NoContent() : result.ToResult();
    }

    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid id, HttpContext httpContext, ISiteCommands commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static IResult GetSettings(HttpContext httpContext, IOptions<SitesConfiguration> configuration)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        return Results.Json(configuration.Value.ToResponse());
    }

    [IgnoreETag]
    public static async Task<IResult> GetHistory(Guid id, HttpContext httpContext, ISiteQueries queries)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        var history = await queries.FindHistory(id, httpContext.RequestAborted);

        return history.Match(() => Results.NotFound(), found => Results.Json(found.ToResponse()));
    }

    [IgnoreETag]
    public static async Task<IResult> GetVersion(Guid id, long version, HttpContext httpContext, ISiteQueries queries)
    {
        httpContext.Response.Headers.CacheControl = NoStore;

        var site = await queries.FindVersion(new VersionedKey<Guid>(id, version), httpContext.RequestAborted);

        return site.Match(() => Results.NotFound(), found => Results.Json(found.ToVersionResponse()));
    }

    [IgnoreETag]
    public static Task<IResult> Create(SaveSiteRequest request, HttpContext httpContext, ISiteCommands commands) =>
        WithDraft(
            request,
            async draft =>
                (await commands.Create(
                    draft.Title,
                    draft.Slug,
                    draft.Content,
                    draft.Publish,
                    httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static Task<IResult> SaveVersion(
        Guid id,
        SaveSiteRequest request,
        HttpContext httpContext,
        ISiteCommands commands) =>
        WithDraft(
            request,
            async draft =>
                (await commands.SaveVersion(
                    id,
                    draft.Title,
                    draft.Slug,
                    draft.Content,
                    draft.Publish,
                    httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static async Task<IResult> Activate(
        Guid id,
        long version,
        HttpContext httpContext,
        ISiteCommands commands)
    {
        var result = await commands.Activate(new VersionedKey<Guid>(id, version), httpContext.RequestAborted);

        return result.ToResult();
    }

    public static Task<IResult> GetPublic(string slug, HttpContext httpContext, ISiteQueries queries) =>
        ServePublic(slug.ToSlug(Site.MaxSlugLength), httpContext, queries);

    public static Task<IResult> GetIndex(
        HttpContext httpContext,
        ISiteQueries queries,
        IOptions<SitesConfiguration> configuration) =>
        ServePublic(configuration.Value.IndexSlugOption, httpContext, queries);

    private static async Task<IResult> ServePublic(Option<string> slug, HttpContext httpContext, ISiteQueries queries)
    {
        httpContext.Response.Headers.CacheControl = NoCache;

        var site = await slug.Match(
            () => Task.FromResult<Option<Site>>(None),
            value => queries.FindActiveBySlug(value, httpContext.RequestAborted));

        return site.Bind(found => found.ToPublicResponse())
            .Match(() => Results.NotFound(), response => Results.Json(response));
    }

    private static Task<IResult> WithDraft(SaveSiteRequest request, Func<SiteDraft, Task<IResult>> action) =>
        request.ToDraft().Match(() => Task.FromResult<IResult>(Results.BadRequest()), action);
}
