using Behind.Common;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SiteImageEndpoints
{
        public const string ImmutableCache = ImageResponseExtensions.ImmutableCache;
    public const string NoSniff = ImageResponseExtensions.NoSniff;

    [IgnoreETag]
    public static Task<IResult> Upload(
        IFormFile? file,
        HttpContext httpContext,
        IVfsCommands commands,
        IOptionsSnapshot<SitesConfiguration> configuration) =>
        Store(VfsTypes.Sites, file, httpContext, commands, configuration.Value.ImageLimit);

    [IgnoreETag]
    public static Task<IResult> UploadNews(
        IFormFile? file,
        HttpContext httpContext,
        IVfsCommands commands,
        IOptionsSnapshot<SitesConfiguration> configuration) =>
        Store(VfsTypes.News, file, httpContext, commands, configuration.Value.ImageLimit);

    [IgnoreETag]
    public static Task<IResult> UploadArticle(
        IFormFile? file,
        HttpContext httpContext,
        IVfsCommands commands,
        IOptionsSnapshot<SitesConfiguration> configuration) =>
        Store(VfsTypes.Articles, file, httpContext, commands, configuration.Value.ImageLimit);

    [IgnoreETag]
    public static Task<IResult> UploadCarousel(
        IFormFile? file,
        HttpContext httpContext,
        IVfsCommands commands,
        IOptionsSnapshot<CarouselsConfiguration> configuration) =>
        Store(VfsTypes.Carousels, file, httpContext, commands, configuration.Value.ImageLimit);

    [IgnoreETag]
    public static Task<IResult> UploadAccess(
        IFormFile? file,
        HttpContext httpContext,
        IVfsCommands commands,
        IOptionsSnapshot<SitesConfiguration> configuration) =>
        Store(VfsTypes.AccessSettings, file, httpContext, commands, configuration.Value.ImageLimit);

    private static async Task<IResult> Store(
        string type,
        IFormFile? file,
        HttpContext httpContext,
        IVfsCommands commands,
        long limit)
    {
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest();
        }

        if (file.Length > limit)
        {
            return Results.Json(new SiteImageLimitResponse(limit), statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var content = await file.ReadAllBytesAsync(httpContext.RequestAborted);

        return await content.DetectImageContentType().Match(
            () => Task.FromResult<IResult>(Results.StatusCode(StatusCodes.Status415UnsupportedMediaType)),
            async contentType =>
            {
                var id = await commands.Add(
                    new IVfsCommands.Upload(
                        type,
                        file.FileName.ToVfsFileName("image"),
                        contentType,
                        content),
                    httpContext.RequestAborted);

                return Results.Json(new SiteImageResponse(id, $"{VfsTypes.ImageUrlOf(type)}/{id}"));
            });
    }

    [IgnoreETag]
    public static Task<IResult> Get(Guid id, HttpContext httpContext, IVfsQueries queries) =>
        Serve(VfsTypes.AllSiteImages, id, httpContext, queries);

    [IgnoreETag]
    public static Task<IResult> GetNews(Guid id, HttpContext httpContext, IVfsQueries queries) =>
        Serve(VfsTypes.NewsImages, id, httpContext, queries);

    [IgnoreETag]
    public static Task<IResult> GetArticle(Guid id, HttpContext httpContext, IVfsQueries queries) =>
        Serve(VfsTypes.ArticleImages, id, httpContext, queries);

    [IgnoreETag]
    public static Task<IResult> GetCarousel(Guid id, HttpContext httpContext, IVfsQueries queries) =>
        Serve(VfsTypes.CarouselImages, id, httpContext, queries);

    [IgnoreETag]
    public static Task<IResult> GetAccess(Guid id, HttpContext httpContext, IVfsQueries queries) =>
        Serve(VfsTypes.AccessImages, id, httpContext, queries);

    private static async Task<IResult> Serve(
        IReadOnlyList<string> types,
        Guid id,
        HttpContext httpContext,
        IVfsQueries queries)
    {
        var image = await queries.Open(types, id, httpContext.RequestAborted);

        return image.Match(
            () => Results.NotFound(),
            found => httpContext.ToImmutableImage(found.File.ContentType, found.Bytes));
    }
}
