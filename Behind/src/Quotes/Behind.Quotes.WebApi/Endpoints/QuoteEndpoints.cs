using System.ComponentModel.DataAnnotations;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Quotes.Database;
using Behind.Quotes.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Quotes.WebApi.Endpoints;

public static class QuoteEndpoints
{
    public const int DefaultPageSize = ManagedListExtensions.DefaultPageSize;
    public const int MaxPageSize = ManagedListExtensions.MaxPageSize;

    public sealed record SaveQuoteRequest(
        [property: Required, MaxLength(Quote.MaxTextLength)] string Text,
        [property: Required, MaxLength(Quote.MaxAuthorLength)] string Author);

    [IgnoreETag]
    public static async Task<IResult> List(
        HttpContext httpContext,
        IQuoteQueries queries,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        httpContext.Response.Headers.CacheControl = ManagedListExtensions.NoStore;

        var quotes = await queries.List(
            Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
            None,
            None,
            DeletionState.Active,
            httpContext.RequestAborted);

        return Results.Json(new QuotesResponse(quotes.ToPageResponse(quote => quote.ToItem())));
    }

    [IgnoreETag]
    public static Task<IResult> Manage(
        HttpContext httpContext,
        IQuoteQueries queries,
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
                var quotes = await queries.List(
                    Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                    query.Search,
                    query.Letter,
                    query.State,
                    httpContext.RequestAborted);

                var letters = await queries.GetLetters(query.Search, query.State, httpContext.RequestAborted);

                return Results.Json(new QuoteManageResponse(
                    quotes.ToPageResponse(quote => quote.ToManaged(query.State)),
                    letters));
            });

    [IgnoreETag]
    public static Task<IResult> Create(
        SaveQuoteRequest request,
        HttpContext httpContext,
        IQuoteCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Create(draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static Task<IResult> Update(
        Guid id,
        SaveQuoteRequest request,
        HttpContext httpContext,
        IQuoteCommands commands) =>
        request.ToDraft().Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async draft => (await commands.Update(id, draft, httpContext.RequestAborted)).ToResult());

    [IgnoreETag]
    public static async Task<IResult> Delete(Guid id, HttpContext httpContext, IQuoteCommands commands) =>
        (await commands.Delete(id, httpContext.RequestAborted)).ToNoContentResult();

    [IgnoreETag]
    public static async Task<IResult> Restore(Guid id, HttpContext httpContext, IQuoteCommands commands) =>
        (await commands.Restore(id, httpContext.RequestAborted)).ToNoContentResult();

    [IgnoreETag]
    public static async Task<IResult> HardDelete(Guid id, HttpContext httpContext, IQuoteCommands commands) =>
        (await commands.HardDelete(id, httpContext.RequestAborted)).ToResult();
}
