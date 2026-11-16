using Behind.Common;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.MarkdownViewer.Common;
using Behind.MarkdownViewer.WebApi.Markdown;
using Behind.MarkdownViewer.WebApi.StaticSite;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using static Behind.Common.FunctionalExtensions;

namespace Behind.MarkdownViewer.WebApi.Endpoints;

public static class MarkdownViewerEndpoints
{
    private const string NoCache = "no-cache";
    private const string IsolatedContent = "default-src 'none'; style-src 'unsafe-inline'; sandbox";

    [IgnoreETag]
    public static Task<IResult> List(
        HttpContext httpContext,
        MarkdownWorkspaceProvider provider,
        [FromQuery] string? locale = null) =>
        provider.Viewing.Match(
            _ => Task.FromResult(Json(new MarkdownDocumentsResponse(
                false,
                MarkdownViewerConfiguration.DefaultTitle,
                MarkdownViewerConfiguration.DefaultDocumentName,
                [],
                null,
                null,
                [],
                []))),
            async workspace =>
            {
                var preferred = workspace.Candidates(locale, provider.DefaultLocale)[0];
                var navigation = await preferred.ListNavigation(httpContext.RequestAborted);

                return Json(new MarkdownDocumentsResponse(
                    true,
                    workspace.Title,
                    preferred.DefaultDocument,
                    workspace.Locales,
                    preferred.LocaleCode,
                    navigation.Home,
                    navigation.Entries,
                    navigation.Sections));
            });

    [IgnoreETag]
    public static Task<IResult> Page(
        string path,
        HttpContext httpContext,
        MarkdownWorkspaceProvider provider,
        [FromQuery] string? locale = null) =>
        provider.Viewing.Match(
            _ => Task.FromResult(Results.NotFound()),
            async workspace => (await workspace.ReadAnyPage(path, locale, provider.DefaultLocale, httpContext.RequestAborted))
                .Match(
                    () => Results.NotFound(),
                    page => Json(page)));

    [IgnoreETag]
    public static Task<IResult> Asset(
        string path,
        HttpContext httpContext,
        MarkdownWorkspaceProvider provider,
        [FromQuery] string? locale = null) =>
        provider.Viewing.Match(
            _ => Task.FromResult(Results.NotFound()),
            async workspace => await workspace.Candidates(locale, provider.DefaultLocale).FindAsset(path).Match(
                () => Task.FromResult(Results.NotFound()),
                file => file.AssetContentType.Match(
                    () => Task.FromResult(Results.NotFound()),
                    async contentType =>
                    {
                        httpContext.Response.Headers.CacheControl = NoCache;
                        httpContext.Response.Headers[HeaderNames.XContentTypeOptions] = ImageResponseExtensions.NoSniff;
                        httpContext.Response.Headers.ContentSecurityPolicy = IsolatedContent;

                        return Results.File(await File.ReadAllBytesAsync(file.FullName, httpContext.RequestAborted), contentType);
                    })));

    [IgnoreETag]
    public static Task<IResult> Generate(
        HttpContext httpContext,
        MarkdownWorkspaceProvider provider,
        ILogger<MarkdownWorkspaceProvider> logger) =>
        provider.Generation.Match(
            errors => Task.FromResult(Results.Json(
                new MarkdownProblemsResponse([..errors.Select(error => error.ToString())]),
                statusCode: StatusCodes.Status400BadRequest)),
            async generation => (await TryAsync(() => generation.Generate(provider.DefaultLocale, httpContext.RequestAborted))).Match(
                exception =>
                {
                    logger.LogError(exception, "Generating the static documentation failed");

                    return Results.Json(
                        new MarkdownProblemsResponse([MarkdownWorkspaceErrors.GenerationFailed]),
                        statusCode: StatusCodes.Status500InternalServerError);
                },
                site => Json(site)));

    private static IResult Json<T>(T value) => Results.Json(value);
}
