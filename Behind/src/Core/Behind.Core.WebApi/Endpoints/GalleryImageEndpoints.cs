using Behind.Common;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Endpoints;

public sealed class GalleryImageEndpoints
{
    public const string ImagePath = "/galleries/images";
    public const int MaxUploadFiles = 20;
    public const long MaxRequestBytes = 128L * 1024 * 1024;
    public const int ThumbnailSide = 400;
    public const int CoverSide = 600;

    [IgnoreETag]
    public static async Task<IResult> Get(Guid id, HttpContext httpContext, IVfsQueries queries)
    {
        var image = await queries.Open(VfsTypes.GalleryImages, id, httpContext.RequestAborted);

        return image.Match(
            () => Results.NotFound(),
            found => httpContext.ToImmutableImage(found.File.ContentType, found.Bytes));
    }

    [IgnoreETag]
    public static async Task<IResult> UploadImages(
        IFormFileCollection? files,
        HttpContext httpContext,
        IVfsCommands commands,
        IOptionsSnapshot<GalleriesConfiguration> configuration)
    {
        if (files is null || files.Count == 0 || files.Count > MaxUploadFiles)
        {
            return Results.BadRequest();
        }

        var uploaded = new List<GalleryUploadedImage>();

        foreach (var file in files)
        {
            uploaded.Add(await Store(file, httpContext, commands, configuration.Value.ImageLimit));
        }

        return Results.Json(new GalleryUploadResponse(uploaded));
    }

    [IgnoreETag]
    public static async Task<IResult> UploadThumbnail(
        IFormFile? file,
        HttpContext httpContext,
        IVfsCommands commands,
        IOptionsSnapshot<GalleriesConfiguration> configuration)
    {
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest();
        }

        var limit = configuration.Value.ImageLimit;

        if (file.Length > limit)
        {
            return Results.Json(new GalleryImageLimitResponse(limit), statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var content = await file.ReadAllBytesAsync(httpContext.RequestAborted);

        return await content.DetectImageContentType().Bind(_ => content.ToThumbnail(CoverSide)).Match(
            () => Task.FromResult<IResult>(Results.StatusCode(StatusCodes.Status415UnsupportedMediaType)),
            async cover =>
            {
                var id = await commands.Add(
                    new IVfsCommands.Upload(
                        VfsTypes.Galleries,
                        file.FileName.ToVfsFileName("thumbnail"),
                        cover.ContentType,
                        cover.Content),
                    httpContext.RequestAborted);

                return Results.Json(new GalleryImageResponse(id, id.ToImageUrl()));
            });
    }

    private static async Task<GalleryUploadedImage> Store(
        IFormFile file,
        HttpContext httpContext,
        IVfsCommands commands,
        long limit)
    {
        var fileName = file.FileName.ToVfsFileName("image");

        if (file.Length == 0)
        {
            return Rejected(fileName, GalleryResponseExtensions.UnreadableImageError);
        }

        if (file.Length > limit)
        {
            return Rejected(fileName, GalleryResponseExtensions.TooLargeImageError);
        }

        var content = await file.ReadAllBytesAsync(httpContext.RequestAborted);

        return await content.DetectImageContentType().Match(
            () => Task.FromResult(Rejected(fileName, GalleryResponseExtensions.UnsupportedImageError)),
            contentType => content.ToThumbnail(ThumbnailSide).Match(
                () => Task.FromResult(Rejected(fileName, GalleryResponseExtensions.UnreadableImageError)),
                async thumbnail =>
                {
                    var imageId = await commands.Add(
                        new IVfsCommands.Upload(VfsTypes.Galleries, fileName, contentType, content),
                        httpContext.RequestAborted);

                    var thumbnailId = await commands.Add(
                        new IVfsCommands.Upload(
                            VfsTypes.Galleries,
                            fileName,
                            thumbnail.ContentType,
                            thumbnail.Content),
                        httpContext.RequestAborted);

                    return new GalleryUploadedImage(
                        fileName,
                        imageId,
                        imageId.ToImageUrl(),
                        thumbnailId,
                        thumbnailId.ToImageUrl(),
                        null);
                }));
    }

    private static GalleryUploadedImage Rejected(string fileName, string error) =>
        new(fileName, null, null, null, null, error);
}
