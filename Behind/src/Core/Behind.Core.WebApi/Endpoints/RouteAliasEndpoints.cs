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

public sealed class RouteAliasEndpoints
{
    public const int DefaultPageSize = ManagedListExtensions.DefaultPageSize;
    public const int MaxPageSize = ManagedListExtensions.MaxPageSize;

    public sealed record SaveRouteAliasRequest(
        [property: Required, MaxLength(RouteAlias.MaxAliasLength * 2)] string Alias,
        [property: Required, MaxLength(RouteAlias.MaxTargetLength)] string Target);

    [IgnoreETag]
    public static async Task<IResult> Resolve(
        string alias,
        HttpContext httpContext,
        IRouteAliasQueries queries)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var wanted = alias.Trim().ToLowerInvariant();
        var target = await queries.FindTarget(wanted, httpContext.RequestAborted);

        return target.Match(
            () => Results.NotFound(),
            found => Results.Json(new RouteAliasTargetResponse(wanted, found)));
    }

    [IgnoreETag]
    public static Task<IResult> Manage(
        HttpContext httpContext,
        IRouteAliasQueries queries,
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
                var aliases = await queries.List(
                    Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                    query.Search,
                    query.Letter,
                    query.State,
                    httpContext.RequestAborted);

                var letters = await queries.GetLetters(query.Search, query.State, httpContext.RequestAborted);

                return Results.Json(new RouteAliasManageResponse(
                    aliases.ToPageResponse(alias => alias.ToItem(query.State)),
                    letters));
            });

    [IgnoreETag]
    public static Task<IResult> Create(
        SaveRouteAliasRequest request,
        HttpContext httpContext,
        IRouteAliasCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Create(draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static Task<IResult> Update(
        Guid id,
        SaveRouteAliasRequest request,
        HttpContext httpContext,
        IRouteAliasCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Update(id, draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid id, HttpContext httpContext, IRouteAliasCommands commands) =>
        await commands.Delete(id, httpContext.RequestAborted) ? Results.NoContent() : Results.NotFound();

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, IRouteAliasCommands commands)
    {
        var result = await commands.Restore(id, httpContext.RequestAborted);

        return result is RouteAliasSaveResult.Saved ? Results.NoContent() : result.ToResult();
    }

    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid id, HttpContext httpContext, IRouteAliasCommands commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();
}
