using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Infrastructure.Localization;
using Behind.Common.WebApi.Middleware;
using Behind.Sport.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Sport.WebApi.Endpoints;

public sealed record SportListResponse<TListing>(PageResponse<TListing> Page, IReadOnlyList<string> Letters);

public static class ResourceEndpoints
{
    public const int DefaultPageSize = ManagedListExtensions.DefaultPageSize;
    public const int MaxPageSize = ManagedListExtensions.MaxPageSize;

    [IgnoreETag]
    public static Task<IResult> List<TListing>(
        HttpContext httpContext,
        IListingQueries<TListing> queries,
        IAuthorizationService authorization,
        [AsParameters] SportListQuery query) =>
        Page<TListing, TListing>(httpContext, queries, authorization, query, false, static listing => listing);

    [IgnoreETag]
    public static Task<IResult> ListPlayers(
        HttpContext httpContext,
        IListingQueries<PlayerListing> queries,
        IAuthorizationService authorization,
        [AsParameters] SportListQuery query) =>
        Page<PlayerListing, PlayerPublicListing>(
            httpContext,
            queries,
            authorization,
            query,
            false,
            PlayerPublicListing.From);

    [IgnoreETag]
    public static Task<IResult> Manage<TListing>(
        HttpContext httpContext,
        IListingQueries<TListing> queries,
        IAuthorizationService authorization,
        [AsParameters] SportListQuery query) =>
        Page<TListing, TListing>(httpContext, queries, authorization, query, query.Deleted, static listing => listing);

    [IgnoreETag]
    public static async Task<IResult> Create<TDraft>(
        TDraft draft,
        HttpContext httpContext,
        IVersionedCommands<TDraft> commands,
        [FromServices] ISportContext? context = null, [FromServices] LocaleCatalog? locales = null)
        where TDraft : IDraft<TDraft> =>
        draft is ContentLocaleDraft selection && !await selection.SelectContentLocale(
            context?.Context as IContentLocaleStore, locales, httpContext.RequestAborted)
            ? Results.BadRequest()
            : (await commands.Create(draft.Normalized(), httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static async Task<IResult> Update<TDraft>(
        Guid id,
        TDraft draft,
        HttpContext httpContext,
        IVersionedCommands<TDraft> commands,
        [FromServices] ISportContext? context = null, [FromServices] LocaleCatalog? locales = null)
        where TDraft : IDraft<TDraft> =>
        draft is ContentLocaleDraft selection && !await selection.SelectContentLocale(
            context?.Context as IContentLocaleStore, locales, httpContext.RequestAborted)
            ? Results.BadRequest()
            : (await commands.Update(id, draft.Normalized(), httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static async Task<IResult> Delete<TDraft>(
        Guid id,
        HttpContext httpContext,
        IVersionedCommands<TDraft> commands) =>
        (await commands.Delete(id, httpContext.RequestAborted)).ToNoContentResult();

    [IgnoreETag]
    public static async Task<IResult> Restore<TDraft>(
        Guid id,
        HttpContext httpContext,
        IVersionedCommands<TDraft> commands) =>
        (await commands.Restore(id, httpContext.RequestAborted)).ToNoContentResult();

    [IgnoreETag]
    public static async Task<IResult> HardDelete<TDraft>(
        Guid id,
        HttpContext httpContext,
        IVersionedCommands<TDraft> commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();

    private static Task<IResult> Page<TListing, TResponse>(
        HttpContext httpContext,
        IListingQueries<TListing> queries,
        IAuthorizationService authorization,
        SportListQuery query,
        bool deleted,
        Func<TListing, TResponse> present) =>
        httpContext.WithManagedList(
            authorization,
            query.Search,
            query.Letter,
            deleted,
            async managed =>
            {
                var request = new ListRequest(
                    Flipper.Create(query.Page, query.PageSize, DefaultPageSize, MaxPageSize),
                    managed.Search,
                    managed.Letter,
                    managed.State,
                    query.ToFilter());

                var page = await queries.List(request, httpContext.RequestAborted);
                var letters = await queries.GetLetters(request, httpContext.RequestAborted);

                return Results.Json(new SportListResponse<TResponse>(page.ToPageResponse(present), letters));
            });
}
