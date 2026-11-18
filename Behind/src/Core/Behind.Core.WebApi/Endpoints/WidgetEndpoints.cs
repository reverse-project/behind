using Behind.Common;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Database;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Endpoints;

public static class WidgetEndpoints
{
    public sealed record HtmlRequest(string? Content);
    public sealed record HtmlResponse(string Content);

    private const int MaxHtmlLength = 100000;

    public static IResult Sanitize(HtmlRequest request)
    {
        var content = request.Content ?? "";

        return content.Length > MaxHtmlLength
            ? Results.BadRequest()
            : Results.Ok(new HtmlResponse(content.SanitizeRichText()));
    }

    public static async Task<IResult> LatestDownloads(IDownloadQueries queries, CancellationToken cancellationToken)
    {
        var all = await queries.Latest(50, cancellationToken);

        var files = all
            .OrderByDescending(file => file.UploadedAt.ToNullable())
            .ThenBy(file => file.Id)
            .Take(50)
            .Select(file => file.ToItem())
            .ToArray();

        return Results.Ok(new DownloadFilesResponse(files));
    }

    [IgnoreETag]
    public static async Task<IResult> LatestNews(
        HttpContext http, INewsQueries queries, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        http.Response.Headers.CacheControl = "no-store";
        var news = await queries.Latest(50, timeProvider.GetUtcNow(), cancellationToken);

        return Results.Ok(news.Select(item => item.ToItem()).ToArray());
    }

}
