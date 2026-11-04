using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Behind.Common;
using Behind.Common.Database.Raw;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Core.WebApi.Endpoints;

public sealed class RawDatabaseEndpoints
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 100;
    public const int MaxColumnNameLength = 128;

    public sealed record InsertRowRequest([property: Required] Dictionary<string, JsonElement> Values);

    public sealed record UpdateRowRequest(
        [property: Required] Dictionary<string, JsonElement> Key,
        [property: Required] Dictionary<string, JsonElement> Values);

    public sealed record DeleteRowRequest([property: Required] Dictionary<string, JsonElement> Key);

    [IgnoreETag]
    public static async Task<IResult> Tables(HttpContext httpContext, IRawDatabaseQueries queries)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        var tables = await queries.ListTables(httpContext.RequestAborted);

        return Results.Json(new RawTablesResponse([..tables.Select(table => table.ToResponse())]));
    }

    [IgnoreETag]
    public static async Task<IResult> Rows(
        string table,
        HttpContext httpContext,
        IRawDatabaseQueries queries,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? search = null,
        [FromQuery] string? orderBy = null,
        [FromQuery] bool descending = false)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        var rows = await queries.ListRows(
            table,
            new RawRowQuery(
                Flipper.Create(page, pageSize, DefaultPageSize, MaxPageSize),
                search.ToSearchTerm(MaxSearchLength),
                orderBy.ToSearchTerm(MaxColumnNameLength),
                descending),
            httpContext.RequestAborted);

        return rows.Match(
            () => Results.NotFound(),
            result => Results.Json(result.ToPageResponse(row => row.Values)));
    }

    [IgnoreETag]
    public static async Task<IResult> Insert(
        string table,
        InsertRowRequest request,
        HttpContext httpContext,
        IRawDatabaseCommands commands) =>
        (await commands.Insert(table, request.Values, httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static async Task<IResult> Update(
        string table,
        UpdateRowRequest request,
        HttpContext httpContext,
        IRawDatabaseCommands commands) =>
        (await commands.Update(table, request.Key, request.Values, httpContext.RequestAborted)).ToResult();

    [IgnoreETag]
    public static async Task<IResult> Delete(
        string table,
        DeleteRowRequest request,
        HttpContext httpContext,
        IRawDatabaseCommands commands) =>
        (await commands.Delete(table, request.Key, httpContext.RequestAborted)).ToResult();
}
